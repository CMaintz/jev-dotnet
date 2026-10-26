using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jev;

/// <summary>Writes an <see cref="Image"/> as its reference and records it for the request's <c>assets</c>.</summary>
internal sealed class ImageConverter : JsonConverter<Image>
{
    public override Image Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("An Image is written as its reference and cannot be read back.");

    public override void Write(Utf8JsonWriter writer, Image value, JsonSerializerOptions options)
    {
        ImageCollector.Record(value);
        writer.WriteStringValue(value.Reference);
    }
}

/// <summary>Writes <see cref="TextWithImages"/> as its text and records its images for the request's <c>assets</c>.</summary>
internal sealed class TextWithImagesConverter : JsonConverter<TextWithImages>
{
    public override TextWithImages Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("TextWithImages is written as text and cannot be read back.");

    public override void Write(Utf8JsonWriter writer, TextWithImages value, JsonSerializerOptions options)
    {
        foreach (var image in value.Images)
        {
            ImageCollector.Record(image);
        }

        writer.WriteStringValue(value.Text);
    }
}
