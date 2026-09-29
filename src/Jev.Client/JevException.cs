namespace Jev;

/// <summary>Base type for every error surfaced by <see cref="JevClient"/>.</summary>
public class JevException : Exception
{
    /// <summary>HTTP status code that triggered the error, when the failure was an HTTP response.</summary>
    public int? StatusCode { get; }

    /// <summary>Raw response body, when one was returned.</summary>
    public string? ResponseBody { get; }

    /// <summary>Create a Jev error.</summary>
    public JevException(string message, int? statusCode = null, string? responseBody = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

/// <summary>401: the API key is missing or invalid.</summary>
public sealed class JevAuthException(string message, string? responseBody = null)
    : JevException(message, 401, responseBody);

/// <summary>422: the request was rejected as malformed.</summary>
public sealed class JevValidationException(string message, string? responseBody = null)
    : JevException(message, 422, responseBody);

/// <summary>429: the rate limit was exceeded and retries were exhausted.</summary>
public sealed class JevRateLimitException(string message, string? responseBody = null)
    : JevException(message, 429, responseBody);

/// <summary>529: the service was overloaded and retries were exhausted.</summary>
public sealed class JevOverloadedException(string message, string? responseBody = null)
    : JevException(message, 529, responseBody);
