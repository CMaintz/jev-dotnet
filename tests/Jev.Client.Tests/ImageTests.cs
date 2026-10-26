using System.Text.Json;
using Xunit;

namespace Jev.Client.Tests;

public sealed class ImageTests
{
    private const string ImagesHeader = "TypeSafe-Danger-Zone-Images";

    private static readonly Dictionary<string, Question> OneNoul = new() { ["q"] = new Noul("Is there a person in `photo`?") };

    private static int s_seed;

    // Only the signature matters: the SDK never decodes an image, the API does.
    private static byte[] Png(int size = 64) => Fake([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], size);

    private static byte[] Jpeg(int size = 64) => Fake([0xFF, 0xD8, 0xFF, 0xE0], size);

    private static byte[] Fake(byte[] magic, int size)
    {
        var bytes = new byte[size];
        new Random(Interlocked.Increment(ref s_seed)).NextBytes(bytes);
        magic.CopyTo(bytes, 0);
        return bytes;
    }

    private static async Task<StubHandler> Send(object state, IReadOnlyDictionary<string, Question> questions)
    {
        var handler = new StubHandler();
        using var client = handler.Client();
        await client.SystemOneAsync(state, questions);
        return handler;
    }

    private static JsonElement RequestJson(StubHandler handler) => JsonDocument.Parse(handler.LastRequestParts[0].Text).RootElement;

    private static Dictionary<string, byte[]> SentImagesByReference(StubHandler handler)
    {
        var parts = handler.LastRequestParts.Skip(1).ToDictionary(p => p.Name!, p => p.Body);
        return RequestJson(handler).GetProperty("assets").EnumerateObject()
            .ToDictionary(asset => asset.Name, asset => parts[asset.Value.GetProperty("part").GetString()!]);
    }

    [Fact]
    public async Task An_image_in_the_state_goes_in_a_file_part_after_the_json_request_part()
    {
        var bytes = Png();
        var photo = Image.Png(bytes);

        var handler = await Send(new Dictionary<string, object> { ["photo"] = photo }, OneNoul);
        var parts = handler.LastRequestParts;
        var json = RequestJson(handler);

        Assert.StartsWith("multipart/form-data", handler.LastRequestHeaders["Content-Type"], StringComparison.Ordinal);
        Assert.Equal("true", handler.LastRequestHeaders.GetValueOrDefault(ImagesHeader));
        Assert.Equal(2, parts.Count);
        Assert.Equal(("request", "application/json"), (parts[0].Name, parts[0].ContentType));
        Assert.Equal(photo.Reference, json.GetProperty("state").GetProperty("photo").GetString());
        var asset = json.GetProperty("assets").GetProperty(photo.Reference);
        Assert.Equal("image", asset.GetProperty("type").GetString());
        Assert.Equal(parts[1].Name, asset.GetProperty("part").GetString());
        Assert.Equal("image/png", parts[1].ContentType);
        Assert.False(string.IsNullOrEmpty(parts[1].FileName));
        Assert.Equal(bytes, parts[1].Body);
    }

    [Fact]
    public async Task Images_in_instructions_and_every_kind_of_criteria_are_each_sent_once()
    {
        var photo = Image.Png(Png());
        var badge = Image.Jpeg(Jpeg());
        var thumb = Image.Png(Png());
        var palm = Image.Png(Png());
        var calm = Image.Jpeg(Jpeg());
        var questions = new Dictionary<string, Question>
        {
            ["same"] = new Noul(
                TextWithImages.Of("Is the person in `photo` the one in ", badge, "?"),
                whenTrue: new { examples = new List<object> { photo } }),
            ["gesture"] = new Choice(
                "Which gesture?",
                new Dictionary<string, object> { ["thumbs_up"] = thumb, ["open"] = TextWithImages.Of("A hand like ", palm) }),
            ["mood"] = new Score("How calm?", [calm, "agitated"]),
        };

        var handler = await Send(new { photo }, questions);
        var sent = SentImagesByReference(handler);

        Assert.Equal(6, handler.LastRequestParts.Count);
        foreach (var image in new[] { photo, badge, thumb, palm, calm })
        {
            Assert.True(sent.ContainsKey(image.Reference), $"missing {image.Reference}");
        }

        Assert.Equal(
            "Is the person in `photo` the one in " + badge.Reference + "?",
            RequestJson(handler).GetProperty("questions").GetProperty("same").GetProperty("instructions").GetString());
        Assert.Equal("image/jpeg", handler.LastRequestParts.Single(p => p.Name == AssetPart(handler, badge)).ContentType);
    }

    [Fact]
    public async Task Each_part_carries_the_bytes_of_the_image_its_asset_names()
    {
        var first = Png();
        var second = Png();
        var images = new[] { Image.Png(first), Image.Png(second) };

        var sent = SentImagesByReference(await Send(images, OneNoul));

        Assert.Equal(first, sent[images[0].Reference]);
        Assert.Equal(second, sent[images[1].Reference]);
    }

    [Fact]
    public async Task A_request_without_images_stays_plain_json()
    {
        var handler = await Send(new { text = "no pictures here" }, OneNoul);

        Assert.StartsWith("application/json", handler.LastRequestHeaders["Content-Type"], StringComparison.Ordinal);
        Assert.False(handler.LastRequestHeaders.ContainsKey(ImagesHeader));
    }

    [Fact]
    public async Task A_multipart_body_is_gzipped_like_any_other()
    {
        var bytes = new byte[8192];
        Png().CopyTo(bytes, 0);

        var handler = await Send(Image.Png(bytes), OneNoul);

        Assert.Equal("gzip", handler.LastRequestHeaders.GetValueOrDefault("Content-Encoding"));
        Assert.Equal(bytes, handler.LastRequestParts[1].Body);
    }

    [Fact]
    public async Task FromFile_tells_png_from_jpeg_by_content_and_rejects_other_formats()
    {
        var dir = Directory.CreateTempSubdirectory("jev-images-");
        try
        {
            // Misleading extensions: the format must come from the bytes.
            var png = Path.Combine(dir.FullName, "photo.jpg");
            var jpeg = Path.Combine(dir.FullName, "photo.png");
            var gif = Path.Combine(dir.FullName, "photo.gif");
            await File.WriteAllBytesAsync(png, Png());
            await File.WriteAllBytesAsync(jpeg, Jpeg());
            await File.WriteAllBytesAsync(gif, "GIF89a............"u8.ToArray());

            var handler = await Send(new[] { Image.FromFile(png), Image.FromFile(jpeg) }, OneNoul);

            Assert.Equal(["image/png", "image/jpeg"], handler.LastRequestParts.Skip(1).Select(p => p.ContentType));
            Assert.Throws<ArgumentException>(() => Image.FromFile(gif));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Image_bytes_are_copied_when_the_image_is_made()
    {
        var bytes = Png();
        var original = bytes.ToArray();
        var image = Image.Png(bytes);
        bytes[^1] ^= 0xFF;

        var handler = await Send(image, OneNoul);

        Assert.Equal(original, handler.LastRequestParts[1].Body);
    }

    [Fact]
    public void Images_and_text_with_images_validate_their_arguments()
    {
        Assert.NotEqual(Image.Png(Png()).Reference, Image.Png(Png()).Reference);
        Assert.Throws<ArgumentException>(() => Image.Png([]));
        Assert.Throws<ArgumentException>(() => Image.Jpeg(new byte[Image.MaxBytes + 1]));
        Assert.Throws<ArgumentException>(() => TextWithImages.Of("a count: ", 3));
        Assert.Throws<ArgumentException>(() => TextWithImages.Of("text", null!));
    }

    private static string AssetPart(StubHandler handler, Image image) =>
        RequestJson(handler).GetProperty("assets").GetProperty(image.Reference).GetProperty("part").GetString()!;
}
