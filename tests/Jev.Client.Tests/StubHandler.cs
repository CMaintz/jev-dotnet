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

    public int Calls { get; private set; }

    public string? LastRequestBody { get; private set; }

    public Uri? LastRequestUri { get; private set; }

    public string? LastAuthorization { get; private set; }

    public StubHandler Enqueue(HttpStatusCode status, string body, TimeSpan? retryAfter = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
        if (retryAfter is { } delay)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(delay);
        }

        _responses.Enqueue(response);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastRequestUri = request.RequestUri;
        LastAuthorization = request.Headers.Authorization?.ToString();
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"answers\":{}}") };
    }
}
