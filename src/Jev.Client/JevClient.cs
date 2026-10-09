using System.Net.Http.Headers;
using Jev.Json;

namespace Jev;

/// <summary>
/// A client for TypeSafe AI's Jev "System One" model. Send a <c>state</c> plus a set
/// of typed <see cref="Question"/>s and get back typed answers with calibrated
/// confidence. Independent questions are evaluated in parallel, so batching them into
/// one call is close to free. Thread-safe; create one and reuse it.
/// </summary>
public sealed class JevClient : IDisposable
{
    private const string EnvKey = "TYPESAFE_API_KEY";

    private readonly HttpClient _http;
    private readonly string _model;
    private readonly RetryingSender _sender;

    /// <summary>Create a client from explicit options.</summary>
    /// <exception cref="ArgumentException">An option is invalid, e.g. a blank model or a base URL with a query.</exception>
    /// <exception cref="JevException">No usable API key is configured or found in <c>TYPESAFE_API_KEY</c>.</exception>
    public JevClient(JevClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var endpoint = Endpoint(options.BaseUrl);
        var apiKey = ResolveApiKey(options.ApiKey);

        // A handler the caller passed in is theirs to dispose; ours is disposed with the client.
        _http = new HttpClient(options.Handler ?? new HttpClientHandler(), disposeHandler: options.Handler is null)
        {
            Timeout = options.Timeout,
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _model = options.Model;
        _sender = new RetryingSender(_http, endpoint, options.MaxRetries, options.TimeProvider);
    }

    /// <summary>Create a client with an explicit API key and otherwise-default options.</summary>
    public JevClient(string apiKey)
        : this(new JevClientOptions { ApiKey = apiKey })
    {
    }

    /// <summary>Create a client that reads its key from <c>TYPESAFE_API_KEY</c>.</summary>
    public static JevClient FromEnvironment() => new(new JevClientOptions());

    /// <summary>
    /// Evaluate <paramref name="questions"/> against <paramref name="state"/>. State may
    /// be a string, or any object/collection serializable to JSON (dictionaries give
    /// exact control over field names). Answers come back keyed by the same ids.
    /// </summary>
    /// <exception cref="ArgumentException">There are no questions, a question is null, or the state cannot be written as JSON.</exception>
    /// <exception cref="JevException">Any network, timeout, HTTP, or response-format failure.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<SystemOneResponse> SystemOneAsync(
        object state,
        IReadOnlyDictionary<string, Question> questions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(questions);
        if (questions.Count == 0 || questions.Values.Any(q => q is null))
        {
            throw new ArgumentException("At least one question is required, and none may be null.", nameof(questions));
        }

        var body = RequestWriter.Serialize(state, _model, questions);
        var json = await _sender.SendAsync(body, cancellationToken).ConfigureAwait(false);
        return ResponseReader.Parse(json, questions);
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    // A leading-slash relative Uri would replace BaseUrl's path, dropping e.g. a proxy prefix.
    private static Uri Endpoint(Uri baseUrl) => new(baseUrl.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/v1/systemone");

    private static string ResolveApiKey(string? configured)
    {
        var key = (configured ?? Environment.GetEnvironmentVariable(EnvKey))?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            throw new JevException($"No API key. Set {EnvKey} or JevClientOptions.ApiKey.");
        }

        if (key.Any(c => c is < '!' or > '~'))
        {
            throw new JevException("The API key may only contain printable ASCII characters.");
        }

        return key;
    }
}
