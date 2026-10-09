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

    public string? LastRequestBody { get; private set; }

    public Uri? LastRequestUri { get; private set; }

    public string? LastAuthorization { get; private set; }

    /// <summary>The clock handed to clients from <see cref="Client"/>; its timers record and fire at once.</summary>
    public RecordingTimeProvider Time { get; } = new();

    /// <summary>Every retry wait the client asked for, in order.</summary>
    public List<TimeSpan> Waits => Time.Waits;

    public StubHandler Enqueue(HttpStatusCode status, string body, RetryConditionHeaderValue? retryAfter = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
        response.Headers.RetryAfter = retryAfter;
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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastRequestUri = request.RequestUri;
        LastAuthorization = request.Headers.Authorization?.ToString();
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        if (_failure is not null)
        {
            throw _failure;
        }

        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"answers\":{}}") };
    }
}
