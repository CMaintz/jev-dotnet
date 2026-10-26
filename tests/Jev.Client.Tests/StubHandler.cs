using System.Net;
using System.Net.Http.Headers;

namespace Jev.Client.Tests;

/// <summary>
/// A test transport that records the last request and replays a scripted queue of
/// responses. Injected via <see cref="JevClientOptions.Handler"/> so tests never
/// touch the network.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private Exception? _failure;

    public int Calls { get; private set; }

    /// <summary>The last request body as text, inflated first when it was sent gzipped, as the API reads it.</summary>
    public string? LastRequestBody { get; private set; }

    /// <summary>The last request body exactly as sent.</summary>
    public byte[]? LastRequestBytes { get; private set; }

    /// <summary>The last request body inflated when it was sent gzipped, as the API reads it.</summary>
    public byte[]? LastRequestContent { get; private set; }

    /// <summary>The parts of the last request, which must be multipart/form-data, read from <see cref="LastRequestContent"/>.</summary>
    public List<MultipartPart> LastRequestParts => MultipartParts.Parse(LastRequestContent!, LastRequestHeaders["Content-Type"]);

    public Uri? LastRequestUri { get; private set; }

    public string? LastAuthorization { get; private set; }

    public HttpMethod? LastRequestMethod { get; private set; }

    /// <summary>The last request's headers, content headers included, each value comma-joined.</summary>
    public Dictionary<string, string> LastRequestHeaders { get; private set; } = [];

    /// <summary>The clock handed to clients from <see cref="Client"/>; its timers record and fire at once.</summary>
    public RecordingTimeProvider Time { get; } = new();

    /// <summary>Every retry wait the client asked for, in order.</summary>
    public List<TimeSpan> Waits => Time.Waits;

    public StubHandler Enqueue(HttpStatusCode status, string body, RetryConditionHeaderValue? retryAfter = null, string? retryAfterMs = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
        response.Headers.RetryAfter = retryAfter;
        if (retryAfterMs is not null)
        {
            response.Headers.Add("retry-after-ms", retryAfterMs);
        }

        _responses.Enqueue(response);
        return this;
    }

    /// <summary>Enqueue a response whose Content-Type declares a charset .NET cannot decode.</summary>
    public StubHandler EnqueueWithBogusCharset(HttpStatusCode status, string body)
    {
        var content = new StringContent(body);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain; charset=utf8x");
        _responses.Enqueue(new HttpResponseMessage(status) { Content = content });
        return this;
    }

    /// <summary>Enqueue a 200 whose UTF-8 body starts with a byte-order mark.</summary>
    public StubHandler EnqueueWithByteOrderMark(string body)
    {
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(body)).ToArray();
        _responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        return this;
    }

    public StubHandler FailWith(Exception failure)
    {
        _failure = failure;
        return this;
    }

    /// <summary>A client on this handler whose retry waits are recorded instead of taken.</summary>
    public JevClient Client(int maxRetries = 3) => new(new JevClientOptions
    {
        ApiKey = "test-key",
        Handler = this,
        MaxRetries = maxRetries,
        TimeProvider = Time,
    });

    private static byte[] Decode(byte[] body, ICollection<string> encodings)
    {
        if (!encodings.Contains("gzip"))
        {
            return body;
        }

        using var gzip = new System.IO.Compression.GZipStream(new MemoryStream(body), System.IO.Compression.CompressionMode.Decompress);
        using var inflated = new MemoryStream();
        gzip.CopyTo(inflated);
        return inflated.ToArray();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastRequestUri = request.RequestUri;
        LastRequestMethod = request.Method;
        LastRequestHeaders = request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        LastAuthorization = request.Headers.Authorization?.ToString();
        LastRequestBytes = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        LastRequestContent = LastRequestBytes is null ? null : Decode(LastRequestBytes, request.Content!.Headers.ContentEncoding);
        LastRequestBody = LastRequestContent is null ? null : System.Text.Encoding.UTF8.GetString(LastRequestContent);
        if (_failure is not null)
        {
            throw _failure;
        }

        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"answers\":{}}") };
    }
}
