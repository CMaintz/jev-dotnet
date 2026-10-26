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
    private const string LongContextHeader = "TypeSafe-Danger-Zone-Long-Context";
    private const string ImagesHeader = "TypeSafe-Danger-Zone-Images";

    // Below this, gzip's framing and the server's inflate step cost more than they save.
    private const int MinCompressedBytes = 1024;

    private static readonly SystemOneOptions DefaultOptions = new();

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _model;
    private readonly bool _compressRequests;
    private readonly Uri _systemOneEndpoint;
    private readonly Uri _modelsEndpoint;
    private readonly RetryingSender _sender;

    /// <summary>Create a client from explicit options.</summary>
    /// <exception cref="ArgumentException">An option is invalid, e.g. a blank model or a base URL with a query.</exception>
    /// <exception cref="JevException">No usable API key is configured or found in <c>TYPESAFE_API_KEY</c>.</exception>
    public JevClient(JevClientOptions options)
        : this(options, http: null)
    {
    }

    /// <summary>
    /// Create a client on an <see cref="HttpClient"/> you own, such as a typed client from
    /// <c>IHttpClientFactory</c> with your own resilience handlers. The client is never
    /// disposed or reconfigured here: its own timeout applies, so <see cref="JevClientOptions.Timeout"/>
    /// is ignored, and <see cref="JevClientOptions.Handler"/> must be left null. Requests go to
    /// <see cref="JevClientOptions.BaseUrl"/>; the client's <see cref="HttpClient.BaseAddress"/> is not used.
    /// </summary>
    /// <exception cref="ArgumentException">An option is invalid, or a <see cref="JevClientOptions.Handler"/> is also set.</exception>
    /// <exception cref="JevException">No usable API key is configured or found in <c>TYPESAFE_API_KEY</c>.</exception>
    public JevClient(HttpClient httpClient, JevClientOptions? options = null)
        : this(options ?? new JevClientOptions(), Required(httpClient))
    {
    }

    private JevClient(JevClientOptions options, HttpClient? http)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (http is not null && options.Handler is not null)
        {
            throw new ArgumentException("Set either an HttpClient or JevClientOptions.Handler, not both.", nameof(options));
        }

        var authorization = new AuthenticationHeaderValue("Bearer", ResolveApiKey(options.ApiKey));
        _ownsHttp = http is null;
        _http = http ?? OwnedClient(options);
        _model = options.Model;
        _compressRequests = options.CompressRequests;
        _systemOneEndpoint = Endpoint(options.BaseUrl, "v1/systemone");
        _modelsEndpoint = Endpoint(options.BaseUrl, "v1/models");
        _sender = new RetryingSender(_http, authorization, options.MaxRetries, options.TimeProvider);
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
    public Task<SystemOneResponse> SystemOneAsync(
        object state,
        IReadOnlyDictionary<string, Question> questions,
        CancellationToken cancellationToken = default) =>
        SystemOneAsync(state, questions, DefaultOptions, cancellationToken);

    /// <summary>
    /// Evaluate <paramref name="questions"/> against <paramref name="state"/> with per-call
    /// <paramref name="options"/>: a different model, or long context, for this request only.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// There are no questions, a question is null, the state cannot be written as JSON, or
    /// <see cref="SystemOneOptions.Model"/> is blank.
    /// </exception>
    /// <exception cref="JevException">Any network, timeout, HTTP, or response-format failure.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<SystemOneResponse> SystemOneAsync(
        object state,
        IReadOnlyDictionary<string, Question> questions,
        SystemOneOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(questions);
        Validate(options);
        if (questions.Count == 0 || questions.Values.Any(q => q is null))
        {
            throw new ArgumentException("At least one question is required, and none may be null.", nameof(questions));
        }

        var written = RequestWriter.Serialize(state, options.Model ?? _model, questions);
        var json = await _sender.SendAsync(SystemOneRequest(written, options, questions), cancellationToken).ConfigureAwait(false);
        return ResponseReader.Parse(json, questions);
    }

    /// <summary>
    /// The models and aliases this account can use, in the order the service lists them.
    /// Pass a returned <see cref="ModelInfo.Name"/> as <see cref="JevClientOptions.Model"/>.
    /// Retried and mapped to typed errors like <c>SystemOneAsync</c>.
    /// </summary>
    /// <exception cref="JevException">Any network, timeout, HTTP, or response-format failure.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        var json = await _sender.SendAsync(ApiRequest.Get(_modelsEndpoint), cancellationToken).ConfigureAwait(false);
        return ModelsReader.Parse(json);
    }

    /// <summary>Dispose the client's own <see cref="HttpClient"/>; one passed in is left open.</summary>
    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private static void Validate(SystemOneOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Model is not null && string.IsNullOrWhiteSpace(options.Model))
        {
            throw new ArgumentException("SystemOneOptions.Model must not be blank; leave it null to use the client's model.", nameof(options));
        }
    }

    // Plain JSON unless there are images to upload; the API wants those as file parts.
    private ApiRequest SystemOneRequest(WrittenRequest written, SystemOneOptions options, IReadOnlyDictionary<string, Question> questions)
    {
        var request = written.Images.Count == 0
            ? ApiRequest.PostJson(_systemOneEndpoint, written.Json)
            : MultipartWriter.Post(_systemOneEndpoint, written);
        return Compressed(request with { Headers = Headers(written, options, questions) });
    }

    private static Dictionary<string, string> Headers(
        WrittenRequest written, SystemOneOptions options, IReadOnlyDictionary<string, Question> questions)
    {
        var headers = new Dictionary<string, string>();
        if (options.LongContext)
        {
            headers[LongContextHeader] = "true";
        }

        if (written.Images.Count > 0 || questions.Values.Any(q => q is Box))
        {
            headers[ImagesHeader] = "true";
        }

        return headers;
    }

    // Compressed once per call, here, because the sender resends these same bytes on a retry.
    private ApiRequest Compressed(ApiRequest request) =>
        _compressRequests && request.Body is { Length: >= MinCompressedBytes } ? request.Gzipped() : request;

    private static HttpClient Required(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return httpClient;
    }

    // A handler the caller passed in is theirs to dispose; ours is disposed with the client.
    private static HttpClient OwnedClient(JevClientOptions options) =>
        new(options.Handler ?? new HttpClientHandler(), disposeHandler: options.Handler is null)
        {
            Timeout = options.Timeout,
        };

    // A leading-slash relative Uri would replace BaseUrl's path, dropping e.g. a proxy prefix.
    private static Uri Endpoint(Uri baseUrl, string path) => new(baseUrl.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/" + path);

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
