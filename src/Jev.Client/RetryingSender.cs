using System.Net.Http.Headers;
using System.Text;

namespace Jev;

/// <summary>
/// Sends an <see cref="ApiRequest"/>, retrying <c>408</c>, <c>429</c> and every <c>5xx</c> (including
/// <c>529</c>) with exponential backoff plus jitter, as TypeSafe's own SDKs do. A
/// <c>retry-after-ms</c> header, else <c>Retry-After</c> (seconds or a date), takes
/// precedence over the computed delay, and every delay is capped. Transport failures and timeouts become a
/// <see cref="JevException"/>; cancellation by the caller's token stays an
/// <see cref="OperationCanceledException"/>.
/// </summary>
internal sealed class RetryingSender
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);
    private const int MaxJitterMilliseconds = 250;

    private readonly HttpClient _http;
    private readonly AuthenticationHeaderValue _authorization;
    private readonly int _maxRetries;
    private readonly TimeProvider _time;

    public RetryingSender(HttpClient http, AuthenticationHeaderValue authorization, int maxRetries, TimeProvider time)
    {
        _http = http;
        _authorization = authorization;
        _maxRetries = maxRetries;
        _time = time;
    }

    /// <summary>The body of the first 2xx response; otherwise the typed <see cref="JevException"/>.</summary>
    public async Task<string> SendAsync(ApiRequest request, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var response = await TrySendAsync(request, ct).ConfigureAwait(false);
            var payload = await ReadBodyAsync(response, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return payload;
            }

            var status = (int)response.StatusCode;
            if (!IsRetryable(status) || attempt >= _maxRetries)
            {
                throw JevException.ForStatus(status, payload);
            }

            var delay = DelayFor(response.Headers, attempt, _time.GetUtcNow());
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

    private static bool IsRetryable(int status) => status is 408 or 429 or >= 500;

    private static TimeSpan DelayFor(HttpResponseHeaders headers, int attempt, DateTimeOffset now)
    {
        var delay = RetryAfterMs(headers) ?? RetryAfter(headers.RetryAfter, now) ?? BaseDelay * Math.Pow(2, Math.Min(attempt, 16));
        var jittered = delay + TimeSpan.FromMilliseconds(Random.Shared.Next(MaxJitterMilliseconds));
        return jittered > MaxDelay ? MaxDelay : jittered;
    }

    private static TimeSpan? RetryAfterMs(HttpResponseHeaders headers) =>
        headers.TryGetValues("retry-after-ms", out var values)
        && long.TryParse(values.FirstOrDefault(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var ms)
            ? TimeSpan.FromMilliseconds(Math.Min(ms, (long)MaxDelay.TotalMilliseconds))
            : null;

    private static TimeSpan? RetryAfter(RetryConditionHeaderValue? header, DateTimeOffset now) => header switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } => date > now ? date - now : TimeSpan.Zero,
        _ => null,
    };

    private async Task<HttpResponseMessage> TrySendAsync(ApiRequest request, CancellationToken ct)
    {
        // Set per request, not on DefaultRequestHeaders, so a caller's shared HttpClient is never mutated.
        using var message = request.ToMessage(_authorization);
        try
        {
            return await _http.SendAsync(message, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            throw new JevException($"Network error calling Jev: {e.Message}", inner: e);
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new JevException("Timed out calling Jev.", inner: e);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // A caller's pipeline (e.g. a Polly circuit breaker) may throw its own types.
            throw new JevException($"Error calling Jev: {e.Message}", inner: e);
        }
    }
}
