namespace Jev;

/// <summary>Configuration for a <see cref="JevClient"/>.</summary>
public sealed class JevClientOptions
{
    /// <summary>API key. If null, the <c>TYPESAFE_API_KEY</c> environment variable is used.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Service base address; <c>/v1/systemone</c> is appended to its path (so a proxy prefix is kept).</summary>
    public Uri BaseUrl { get; set; } = new("https://api.typesafe.ai");

    /// <summary>Model id. <c>jev-latest</c> tracks the current recommended model.</summary>
    public string Model { get; set; } = "jev-latest";

    /// <summary>Per-request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Maximum retries after a 429 or 529 before giving up.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Transport override. Inject a stub <see cref="HttpMessageHandler"/> to test
    /// without a network; leave null for the default handler.
    /// </summary>
    public HttpMessageHandler? Handler { get; set; }
}
