using System.Text;

namespace Jev;

/// <summary>
/// Text with images inside it, such as "Is this the dog in [photo]?". Build it with
/// <see cref="Of"/>; use it anywhere text can go, like an <see cref="Image"/>. It is sent as
/// <see cref="Text"/>, where each image appears as its <see cref="Image.Reference"/>.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(TextWithImagesConverter))]
public sealed record TextWithImages
{
    private TextWithImages(string text, IReadOnlyList<Image> images)
    {
        Text = text;
        Images = images;
    }

    /// <summary>The text as sent, with each image's reference in its place.</summary>
    public string Text { get; }

    /// <summary>The images in the text, in order.</summary>
    public IReadOnlyList<Image> Images { get; }

    /// <summary>Join <paramref name="parts"/>, each a <see cref="string"/> or an <see cref="Image"/>, into one text.</summary>
    /// <exception cref="ArgumentException">A part is null or neither text nor an image.</exception>
    public static TextWithImages Of(params object[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var text = new StringBuilder();
        var images = new List<Image>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case string s:
                    text.Append(s);
                    break;
                case Image image:
                    text.Append(image.Reference);
                    images.Add(image);
                    break;
                default:
                    throw new ArgumentException("Each part must be a string or an Image.", nameof(parts));
            }
        }

        return new TextWithImages(text.ToString(), images.AsReadOnly());
    }

    /// <inheritdoc />
    public bool Equals(TextWithImages? other) =>
        other is not null && Text == other.Text && ContentEquality.SameItems(Images, other.Images);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Text, Images.Count);
}
