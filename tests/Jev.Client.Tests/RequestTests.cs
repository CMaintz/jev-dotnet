using System.Net;
using System.Text.Json;
using Xunit;

namespace Jev.Client.Tests;

public sealed class RequestTests
{
    private static JevClient ClientWith(StubHandler handler) =>
        new(new JevClientOptions { ApiKey = "test-key", Handler = handler });

    private static async Task<JsonElement> CapturedRequest(StubHandler handler, IReadOnlyDictionary<string, Question> questions, object state)
    {
        handler.Enqueue(HttpStatusCode.OK, "{\"answers\":{}}");
        using var client = ClientWith(handler);
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
        Assert.True(root.GetProperty("questions").TryGetProperty("q", out _));
        Assert.Equal("Bearer test-key", handler.LastAuthorization);
        Assert.EndsWith("/v1/systemone", handler.LastRequestUri!.AbsolutePath, StringComparison.Ordinal);
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
        using var client = ClientWith(new StubHandler());
        await Assert.ThrowsAsync<ArgumentException>(() => client.SystemOneAsync("s", new Dictionary<string, Question>()));
    }

    [Fact]
    public void Score_requires_two_to_ten_levels()
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
