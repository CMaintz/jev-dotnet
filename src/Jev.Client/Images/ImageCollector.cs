namespace Jev;

/// <summary>
/// Records every <see cref="Image"/> the serializer writes while a scope is open, so the
/// request can list it in <c>assets</c>. The converters only see the value they write, and
/// a scope per call keeps the shared <see cref="System.Text.Json.JsonSerializerOptions"/>
/// free of per-request state. Thread-static works because serialization is synchronous and
/// each scope opens and closes on the same thread.
/// </summary>
internal sealed class ImageCollector : IDisposable
{
    [ThreadStatic]
    private static ImageCollector? t_current;

    private readonly ImageCollector? _outer;
    private readonly ImageSet _into;

    private ImageCollector(ImageSet into)
    {
        _outer = t_current;
        _into = into;
        t_current = this;
    }

    /// <summary>Collect into <paramref name="into"/> until the returned scope is disposed.</summary>
    public static ImageCollector Into(ImageSet into) => new(into);

    /// <summary>Record <paramref name="image"/> in the open scope.</summary>
    /// <exception cref="InvalidOperationException">No scope is open: the image is being serialized
    /// on its own, outside a request, so its file would never be uploaded.</exception>
    public static void Record(Image image)
    {
        if (t_current is null)
        {
            throw new InvalidOperationException(
                "An Image cannot be serialized on its own: that keeps only its reference text and drops the file. " +
                "Pass the Image itself in the state or a question.");
        }

        t_current._into.Add(image);
    }

    public void Dispose() => t_current = _outer;
}
