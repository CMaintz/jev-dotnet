namespace Jev;

/// <summary>Configuration for a <see cref="JevClient"/>, validated when the client is created.</summary>
public sealed class JevClientOptions
{
    /// <summary>API key; surrounding whitespace is trimmed. If null, the <c>TYPESAFE_API_KEY</c> environment variable is used.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Service base address: an absolute http(s) URL without a query or fragment.
    /// <c>/v1/systemone</c> and <c>/v1/models</c> are appended to its path, so a proxy prefix is kept.
    /// </summary>
    public Uri BaseUrl { get; set; } = new("https://api.typesafe.ai");

    /// <summary>Model id. <c>jev-latest</c> tracks the current recommended model.</summary>
    public string Model { get; set; } = "jev-latest";

    /// <summary>
    /// Per-request timeout; positive and at most <see cref="int.MaxValue"/> milliseconds, or <see cref="Timeout.InfiniteTimeSpan"/>.
    /// Ignored when you pass in your own <see cref="HttpClient"/>, whose timeout applies instead.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gzip request bodies of 1024 bytes or more and send them with <c>Content-Encoding: gzip</c>,
    /// which TypeSafe's API accepts. On by default; turn it off when a proxy or gateway at
    /// <see cref="BaseUrl"/> does not accept compressed request bodies.
    /// </summary>
    public bool CompressRequests { get; set; } = true;

    /// <summary>Maximum retries after a 408, 429 or 5xx before giving up; zero disables retrying.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Handler override. Inject a stub <see cref="HttpMessageHandler"/> to test
    /// without a network; leave null for the default handler. A handler you pass in is
    /// not disposed by the client. Must be null when you pass in your own <see cref="HttpClient"/>.
    /// </summary>
    public HttpMessageHandler? Handler { get; set; }

    /// <summary>Clock for retry waits and <c>Retry-After</c> dates; defaults to <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Model, nameof(Model));
        ArgumentOutOfRangeException.ThrowIfNegative(MaxRetries, nameof(MaxRetries));
        ArgumentNullException.ThrowIfNull(TimeProvider, nameof(TimeProvider));
        var infinite = Timeout == System.Threading.Timeout.InfiniteTimeSpan;
        if (!infinite && (Timeout <= TimeSpan.Zero || Timeout.TotalMilliseconds > int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout), Timeout, "Timeout must be positive and at most int.MaxValue milliseconds.");
        }

        ValidateBaseUrl();
    }

    private void ValidateBaseUrl()
    {
        ArgumentNullException.ThrowIfNull(BaseUrl, nameof(BaseUrl));
        var valid = BaseUrl.IsAbsoluteUri
            && (BaseUrl.Scheme == Uri.UriSchemeHttps || BaseUrl.Scheme == Uri.UriSchemeHttp)
            && string.IsNullOrEmpty(BaseUrl.Query)
            && string.IsNullOrEmpty(BaseUrl.Fragment);
        if (!valid)
        {
            throw new ArgumentException(
                $"BaseUrl must be an absolute http(s) URL without a query or fragment: {BaseUrl}", nameof(BaseUrl));
        }
    }
}
