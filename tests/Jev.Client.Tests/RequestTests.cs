using System.Net;
using System.Text.Json;
using Xunit;

namespace Jev.Client.Tests;

public sealed class RequestTests
{
    private static async Task<JsonElement> CapturedRequest(StubHandler handler, IReadOnlyDictionary<string, Question> questions, object state)
    {
        handler.Enqueue(HttpStatusCode.OK, "{\"answers\":{}}");
        using var client = handler.Client();
        await client.SystemOneAsync(state, questions);
        return JsonDocument.Parse(handler.LastRequestBody!).RootElement.Clone();
    }

    [Fact]
    public async Task Sends_model_state_questions_auth_and_endpoint()
    {
        var handler = new StubHandler();
        var state = new Dictionary<string, object> { ["text"] = "hello" };
        var root = await CapturedRequest(handler, new Dictionary<string, Question> { ["q"] = new Noul("Refund?") }, state);

        Assert.Equal("jev-latest", root.GetProperty("model").GetString());
        Assert.Equal("hello", root.GetProperty("state").GetProperty("text").GetString());
        Assert.Equal("noul", root.GetProperty("questions").GetProperty("q").GetProperty("type").GetString());
        Assert.Equal("Bearer test-key", handler.LastAuthorization);
        Assert.EndsWith("/v1/systemone", handler.LastRequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://api.typesafe.ai", "https://api.typesafe.ai/v1/systemone")]
    [InlineData("https://gateway.example.com/typesafe", "https://gateway.example.com/typesafe/v1/systemone")]
    [InlineData("https://gateway.example.com/typesafe/", "https://gateway.example.com/typesafe/v1/systemone")]
    public async Task Endpoint_keeps_the_base_url_path(string baseUrl, string expected)
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.OK, "{\"answers\":{}}");
        using var client = new JevClient(new JevClientOptions { ApiKey = "k", Handler = handler, BaseUrl = new Uri(baseUrl) });

        await client.SystemOneAsync("s", new Dictionary<string, Question> { ["q"] = new Noul("Refund?") });

        Assert.Equal(new Uri(expected), handler.LastRequestUri);
    }

    [Fact]
    public async Task Choice_criteria_is_a_map_score_is_an_array()
    {
        var questions = new Dictionary<string, Question>
        {
            ["team"] = new Choice("Which team", new Dictionary<string, string> { ["billing"] = "pay", ["tech"] = "bugs" }),
            ["anger"] = new Score("How angry", ["calm", "cross", "furious"]),
        };
        var root = await CapturedRequest(new StubHandler(), questions, "s");
        var qs = root.GetProperty("questions");

        Assert.Equal(JsonValueKind.Object, qs.GetProperty("team").GetProperty("criteria").ValueKind);
        Assert.Equal("bugs", qs.GetProperty("team").GetProperty("criteria").GetProperty("tech").GetString());
        Assert.Equal(JsonValueKind.Array, qs.GetProperty("anger").GetProperty("criteria").ValueKind);
        Assert.Equal(3, qs.GetProperty("anger").GetProperty("criteria").GetArrayLength());
    }

    [Fact]
    public async Task Noul_emits_criteria_only_when_glossed()
    {
        var questions = new Dictionary<string, Question>
        {
            ["bare"] = new Noul("Plain?"),
            ["glossed"] = new Noul("Refund?", whenTrue: "asks for money back"),
        };
        var qs = (await CapturedRequest(new StubHandler(), questions, "s")).GetProperty("questions");

        Assert.False(qs.GetProperty("bare").TryGetProperty("criteria", out _));
        Assert.Equal("asks for money back", qs.GetProperty("glossed").GetProperty("criteria").GetProperty("true").GetString());
    }

    [Fact]
    public async Task Empty_questions_is_rejected()
    {
        using var client = new StubHandler().Client();
        await Assert.ThrowsAsync<ArgumentException>(() => client.SystemOneAsync("s", new Dictionary<string, Question>()));
    }

    [Fact]
    public async Task Unwritable_state_is_rejected_before_sending()
    {
        var handler = new StubHandler();
        using var client = handler.Client();
        var questions = new Dictionary<string, Question> { ["q"] = new Noul("Refund?") };

        await Assert.ThrowsAsync<ArgumentException>(() => client.SystemOneAsync(double.NaN, questions));
        Assert.Equal(0, handler.Calls);
        await Assert.ThrowsAsync<ArgumentException>(() => client.SystemOneAsync("s", new Dictionary<string, Question> { ["q"] = null! }));
    }

    [Fact]
    public async Task Choice_options_keep_the_callers_order()
    {
        var options = new Dictionary<string, string>();
        foreach (var option in new[] { "zeta", "alpha", "mid", "beta", "omega" })
        {
            options[option] = "about " + option;
        }

        var root = await CapturedRequest(
            new StubHandler(), new Dictionary<string, Question> { ["q"] = new Choice("Pick", options) }, "s");
        var sent = root.GetProperty("questions").GetProperty("q").GetProperty("criteria").EnumerateObject().Select(p => p.Name);

        Assert.Equal(options.Keys, sent);
    }

    [Fact]
    public void Questions_validate_their_arguments()
    {
        Assert.Throws<ArgumentException>(() => new Noul(" "));
        Assert.Throws<ArgumentException>(() => new Choice("x", new Dictionary<string, string> { ["a"] = " " }));
        Assert.Throws<ArgumentException>(() => new Score("x", ["low", ""]));
        var tooMany = Enumerable.Range(0, Choice.MaxOptions + 1).ToDictionary(i => $"o{i}", i => $"option {i}");
        Assert.Throws<ArgumentException>(() => new Choice("x", tooMany));
    }

    [Fact]
    public void Options_are_validated_when_the_client_is_created()
    {
        Assert.Throws<ArgumentException>(() => new JevClient(new JevClientOptions { ApiKey = "k", Model = " " }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevClient(new JevClientOptions { ApiKey = "k", MaxRetries = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevClient(new JevClientOptions { ApiKey = "k", Timeout = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevClient(new JevClientOptions { ApiKey = "k", Timeout = TimeSpan.FromDays(30) }));
        Assert.Throws<ArgumentException>(() => new JevClient(
            new JevClientOptions { ApiKey = "k", BaseUrl = new Uri("https://proxy.example.com?x=1") }));
        Assert.Throws<ArgumentException>(() => new JevClient(
            new JevClientOptions { ApiKey = "k", BaseUrl = new Uri("ftp://example.com") }));
        Assert.Throws<JevException>(() => new JevClient(new JevClientOptions { ApiKey = "sk-abc€" }));
    }

    [Fact]
    public async Task Api_key_whitespace_is_trimmed()
    {
        var handler = new StubHandler();
        using var client = new JevClient(new JevClientOptions { ApiKey = " secret\n", Handler = handler });

        await client.SystemOneAsync("s", new Dictionary<string, Question> { ["q"] = new Noul("Refund?") });

        Assert.Equal("Bearer secret", handler.LastAuthorization);
    }

    [Fact]
    public async Task A_handler_passed_in_is_not_disposed_with_the_client()
    {
        var handler = new StubHandler();
        new JevClient(new JevClientOptions { ApiKey = "k", Handler = handler }).Dispose();

        using var again = handler.Client();
        await again.SystemOneAsync("s", new Dictionary<string, Question> { ["q"] = new Noul("Refund?") });
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Score_and_choice_require_a_valid_number_of_levels()
    {
        Assert.Throws<ArgumentException>(() => new Score("x", ["only one"]));
        Assert.Throws<ArgumentException>(() => new Choice("x", new Dictionary<string, string>()));
    }

    [Fact]
    public void Missing_api_key_is_rejected()
    {
        Assert.Throws<JevException>(() => new JevClient(new JevClientOptions { ApiKey = "   " }));
    }
}
