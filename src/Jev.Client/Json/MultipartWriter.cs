using System.Net.Http.Headers;

namespace Jev.Json;

/// <summary>
/// Encodes a request with images as <c>multipart/form-data</c>, which TypeSafe recommends
/// over base64 because it sends each file as is: the JSON body first, in a part named
/// <c>request</c>, then one file part per image under the name its asset gives.
/// </summary>
internal static class MultipartWriter
{
    public static ApiRequest Post(Uri uri, WrittenRequest written)
    {
        var boundary = "jev-" + Guid.NewGuid().ToString("N");
        using var content = new MultipartFormDataContent(boundary);
        content.Add(Part(written.Json, "application/json", "request", fileName: null));
        for (var i = 0; i < written.Images.Count; i++)
        {
            var image = written.Images[i];
            var name = RequestWriter.PartName(i);
            content.Add(Part(image.Bytes, image.MediaType, name, name + image.FileExtension));
        }

        // Rendered once to bytes, so a retry resends the same body and the boundary stays put.
        using var buffer = new MemoryStream();
        content.CopyTo(buffer, context: null, CancellationToken.None);
        return new ApiRequest(HttpMethod.Post, uri) { Body = buffer.ToArray(), ContentType = MultipartType(boundary) };
    }

    // The base Add keeps these headers as set; MultipartFormDataContent.Add(content, name, fileName)
    // would also emit an RFC 5987 filename* parameter that some form parsers mishandle.
    private static ByteArrayContent Part(byte[] bytes, string mediaType, string name, string? fileName)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = $"\"{name}\"",
            FileName = fileName is null ? null : $"\"{fileName}\"",
        };
        return part;
    }

    // The boundary is hex, so it needs no quotes; MultipartContent's own header would add them.
    private static MediaTypeHeaderValue MultipartType(string boundary)
    {
        var type = new MediaTypeHeaderValue("multipart/form-data");
        type.Parameters.Add(new NameValueHeaderValue("boundary", boundary));
        return type;
    }
}
