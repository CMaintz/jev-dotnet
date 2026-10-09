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

    /// <summary>The typed exception for a non-2xx <paramref name="status"/>.</summary>
    internal static JevException ForStatus(int status, string body) => status switch
    {
        401 => new JevAuthException("Unauthorized: invalid or missing API key.", body),
        422 => new JevValidationException("Unprocessable entity: the request was rejected as malformed.", body),
        429 => new JevRateLimitException("Rate limit exceeded; retries exhausted.", body),
        529 => new JevOverloadedException("Service overloaded; retries exhausted.", body),
        _ => new JevException($"Unexpected HTTP {status} from Jev.", status, body),
    };
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
