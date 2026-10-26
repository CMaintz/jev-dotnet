namespace Jev;

/// <summary>
/// A PNG or JPEG image to send in a request. Put it anywhere text can go: as the whole
/// state, as any value inside it, as a question's instructions, or as a criteria value
/// (a Choice option, a Score level, a Noul gloss); use <see cref="TextWithImages"/> to put
/// one inside a sentence. Each image travels once per request, however often it is used.
/// </summary>
/// <remarks>
/// Images are sent as is: the SDK does not decode, resize or rotate them. TypeSafe accepts
/// still PNG and JPEG images of 280 to 1568 pixels per side, at most 16 MiB per file and
/// 32 MiB per request, and rejects anything outside that with a 400 or 413; see
/// <see href="https://docs.typesafe.ai/images.md">Images</see>. TypeSafe calls images
/// "dreamer-ready": experimental, with a format and limits that may change. Images compare
/// by identity: two images with the same bytes are still two images.
/// <para>
/// Pass the image itself, inside the state or a question. An image you serialize first (say
/// into a <see cref="System.Text.Json.JsonElement"/>) is only its reference string: the
/// file is not uploaded, and the model reads the reference as plain text.
/// </para>
/// </remarks>
[System.Text.Json.Serialization.JsonConverter(typeof(ImageConverter))]
public sealed class Image
{
    /// <summary>The largest image file TypeSafe accepts: 16 MiB.</summary>
    public const int MaxBytes = 16 * 1024 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    private Image(byte[] bytes, string mediaType, string extension)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length is 0 or > MaxBytes)
        {
            throw new ArgumentException($"An image must hold between 1 byte and {MaxBytes} bytes (16 MiB).", nameof(bytes));
        }

        Bytes = bytes.ToArray();
        MediaType = mediaType;
        FileExtension = extension;
    }

    /// <summary>
    /// The text that stands for this image in the request: a unique <c>image-</c> prefixed
    /// UUID, written wherever the image is placed and used as its key in <c>assets</c>.
    /// </summary>
    public string Reference { get; } = "image-" + Guid.NewGuid().ToString("D");

    internal byte[] Bytes { get; }

    internal string MediaType { get; }

    internal string FileExtension { get; }

    /// <summary>A PNG image; <paramref name="bytes"/> are copied.</summary>
    /// <exception cref="ArgumentException">The bytes are empty or over <see cref="MaxBytes"/>.</exception>
    public static Image Png(byte[] bytes) => new(bytes, "image/png", ".png");

    /// <summary>A JPEG image; <paramref name="bytes"/> are copied.</summary>
    /// <exception cref="ArgumentException">The bytes are empty or over <see cref="MaxBytes"/>.</exception>
    public static Image Jpeg(byte[] bytes) => new(bytes, "image/jpeg", ".jpg");

    /// <summary>The image file at <paramref name="path"/>, PNG or JPEG as its content says, whatever its extension.</summary>
    /// <exception cref="ArgumentException">The file is neither PNG nor JPEG, is empty, or is over <see cref="MaxBytes"/>.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static Image FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (new FileInfo(path).Length > MaxBytes)
        {
            throw new ArgumentException($"{path} is over {MaxBytes} bytes (16 MiB).", nameof(path));
        }

        var bytes = File.ReadAllBytes(path);
        return bytes.AsSpan().StartsWith(PngSignature) ? Png(bytes)
            : bytes.AsSpan().StartsWith(JpegSignature) ? Jpeg(bytes)
            : throw new ArgumentException($"{path} is not a PNG or JPEG image.", nameof(path));
    }
}
