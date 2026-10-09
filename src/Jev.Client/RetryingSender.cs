using System.Net.Http.Headers;
using System.Text;

namespace Jev;

/// <summary>
/// Posts a request body, retrying <c>429</c> and <c>529</c> with exponential backoff plus
/// jitter. A <c>Retry-After</c> header (seconds or a date) takes precedence over the
/// computed delay, and every delay is capped. Transport failures and timeouts become a
/// <see cref="JevException"/>; cancellation by the caller's token stays an
/// <see cref="OperationCanceledException"/>.
/// </summary>
internal sealed class RetryingSender
{
    private static readonly HashSet<int> Retryable = [429, 529];
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);
    private const int MaxJitterMilliseconds = 250;

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly int _maxRetries;
    private readonly TimeProvider _time;

    public RetryingSender(HttpClient http, Uri endpoint, int maxRetries, TimeProvider time)
    {
        _http = http;
        _endpoint = endpoint;
        _maxRetries = maxRetries;
        _time = time;
    }

    /// <summary>The body of the first 2xx response; otherwise the typed <see cref="JevException"/>.</summary>
    public async Task<string> SendAsync(byte[] body, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var response = await PostAsync(body, ct).ConfigureAwait(false);
            var payload = await ReadBodyAsync(response, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return payload;
            }

            var status = (int)response.StatusCode;
            if (!Retryable.Contains(status) || attempt >= _maxRetries)
            {
                throw JevException.ForStatus(status, payload);
            }

            var delay = DelayFor(response.Headers.RetryAfter, attempt, _time.GetUtcNow());
            await Task.Delay(delay, _time, ct).ConfigureAwait(false);
        }
    }

    // Decoded as UTF-8 regardless of the declared charset, which a misconfigured proxy may get
    // wrong; a leading byte-order mark is dropped, as ReadAsStringAsync would.
    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct) =>
        DecodeUtf8(await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false));

    private static string DecodeUtf8(byte[] bytes)
    {
        var start = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? Encoding.UTF8.Preamble.Length : 0;
        return Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
    }

    private static TimeSpan DelayFor(RetryConditionHeaderValue? retryAfter, int attempt, DateTimeOffset now)
    {
        var delay = RetryAfter(retryAfter, now) ?? BaseDelay * Math.Pow(2, Math.Min(attempt, 16));
        var jittered = delay + TimeSpan.FromMilliseconds(Random.Shared.Next(MaxJitterMilliseconds));
        return jittered > MaxDelay ? MaxDelay : jittered;
    }

    private static TimeSpan? RetryAfter(RetryConditionHeaderValue? header, DateTimeOffset now) => header switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } => date > now ? date - now : TimeSpan.Zero,
        _ => null,
    };

    private async Task<HttpResponseMessage> PostAsync(byte[] body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        try
        {
            return await _http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            throw new JevException($"Network error calling Jev: {e.Message}", inner: e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new JevException("Timed out calling Jev.", inner: e);
        }
    }
}
