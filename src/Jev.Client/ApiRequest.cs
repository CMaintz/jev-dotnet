using System.IO.Compression;
using System.Net.Http.Headers;

namespace Jev;

/// <summary>
/// One API call, described rather than built: an <see cref="HttpRequestMessage"/> cannot
/// be sent twice, so <see cref="RetryingSender"/> builds a fresh one for every attempt
/// from the same body bytes.
/// </summary>
internal sealed record ApiRequest(HttpMethod Method, Uri Uri)
{
    /// <summary>The body, or null for a request without one.</summary>
    public byte[]? Body { get; init; }

    /// <summary>The body's media type; required with <see cref="Body"/>.</summary>
    public MediaTypeHeaderValue? ContentType { get; init; }

    /// <summary>The body's <c>Content-Encoding</c>, or null when it is sent as is.</summary>
    public string? ContentEncoding { get; init; }

    /// <summary>Extra request headers, such as TypeSafe's opt-in <c>Danger-Zone</c> flags.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    public static ApiRequest Get(Uri uri) => new(HttpMethod.Get, uri);

    public static ApiRequest PostJson(Uri uri, byte[] body) =>
        new(HttpMethod.Post, uri) { Body = body, ContentType = new MediaTypeHeaderValue("application/json") };

    /// <summary>This request with its body gzipped and labelled as such.</summary>
    public ApiRequest Gzipped()
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal))
        {
            gzip.Write(Body);
        }

        return this with { Body = buffer.ToArray(), ContentEncoding = "gzip" };
    }

    public HttpRequestMessage ToMessage(AuthenticationHeaderValue authorization)
    {
        var message = new HttpRequestMessage(Method, Uri);
        message.Headers.Authorization = authorization;
        foreach (var (name, value) in Headers)
        {
            message.Headers.Add(name, value);
        }

        if (Body is not null)
        {
            message.Content = new ByteArrayContent(Body);
            message.Content.Headers.ContentType = ContentType;
            if (ContentEncoding is not null)
            {
                message.Content.Headers.ContentEncoding.Add(ContentEncoding);
            }
        }

        return message;
    }
}
