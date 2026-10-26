namespace Jev;

/// <summary>Base type for every error this SDK surfaces, from <see cref="JevClient"/> or <see cref="JevThresholds"/>.</summary>
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
        400 => new JevBadRequestException("Bad request: the body broke a request rule.", body),
        401 => new JevAuthException("Unauthorized: invalid or missing API key.", body),
        413 => new JevPayloadTooLargeException("Payload too large: the body is over 32 MiB.", body),
        422 => new JevValidationException("Unprocessable entity: the request was rejected as malformed.", body),
        429 => new JevRateLimitException("Rate limit exceeded; retries exhausted.", body),
        529 => new JevOverloadedException("Service overloaded; retries exhausted.", body),
        >= 500 => new JevServerException($"Jev failed with HTTP {status}; retries exhausted.", status, body),
        _ => new JevException($"Unexpected HTTP {status} from Jev.", status, body),
    };
}

/// <summary>
/// 400: the request broke one of the API's request rules, such as too much nesting or a
/// number that is not finite. With <c>max_tokens_exceeded</c> in the body, the request is
/// longer than the model's context length.
/// </summary>
public sealed class JevBadRequestException(string message, string? responseBody = null)
    : JevException(message, 400, responseBody);

/// <summary>413: the request body is over the API's 32 MiB limit.</summary>
public sealed class JevPayloadTooLargeException(string message, string? responseBody = null)
    : JevException(message, 413, responseBody);

/// <summary>A 5xx other than 529: the service failed and retries were exhausted. The exact status is on <see cref="JevException.StatusCode"/>.</summary>
public sealed class JevServerException(string message, int statusCode, string? responseBody = null)
    : JevException(message, statusCode, responseBody);

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
