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

    public static ApiRequest Get(Uri uri) => new(HttpMethod.Get, uri);

    public static ApiRequest PostJson(Uri uri, byte[] body) =>
        new(HttpMethod.Post, uri) { Body = body, ContentType = new MediaTypeHeaderValue("application/json") };

    public HttpRequestMessage ToMessage(AuthenticationHeaderValue authorization)
    {
        var message = new HttpRequestMessage(Method, Uri);
        message.Headers.Authorization = authorization;
        if (Body is not null)
        {
            message.Content = new ByteArrayContent(Body);
            message.Content.Headers.ContentType = ContentType;
        }

        return message;
    }
}
