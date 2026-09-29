using System.Net;
using Xunit;

namespace Jev.Client.Tests;

public sealed class ResponseTests
{
    private static readonly Dictionary<string, Question> OneNoul = new() { ["q"] = new Noul("Refund?") };

    private static JevClient ClientWith(StubHandler handler, int maxRetries = 3) =>
        new(new JevClientOptions { ApiKey = "test-key", Handler = handler, MaxRetries = maxRetries });

    private static async Task<SystemOneResponse> Parse(string body)
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.OK, body);
        using var client = ClientWith(handler);
        return await client.SystemOneAsync("s", OneNoul);
    }

    [Fact]
    public async Task Reads_a_choice_answer_with_probability_map()
    {
        const string body = """
        {"model":"jev-1","answers":{"team":{"type":"choice","choice":"tech",
        "probabilities":{"billing":0.1,"tech":0.9},"confidence":0.8}},
        "usage":{"input_tokens":10,"output_tokens":2}}
        """;
        var resp = await Parse(body);
        var a = resp["team"];

        Assert.Equal("choice", a.Type);
        Assert.Equal("tech", a.ChoiceValue);
        Assert.Equal(0.9, a.Probabilities!["tech"]);
        Assert.Null(a.ScoreProbabilities);
        Assert.True(a.IsConfident(0.7));
        Assert.Equal(10, resp.Usage!.InputTokens);
        Assert.Single(resp.Choices);
    }

    [Fact]
    public async Task Reads_a_score_answer_with_probability_array_and_legend()
    {
        const string body = """
        {"answers":{"anger":{"type":"score","score":1.5,
        "legend":{"0":"calm","1":"cross","2":"furious"},
        "probabilities":[0.2,0.3,0.5],"confidence":0.6}}}
        """;
        var a = (await Parse(body))["anger"];

        Assert.Equal(1.5, a.ScoreValue);
        Assert.Equal(3, a.ScoreProbabilities!.Count);
        Assert.Null(a.Probabilities);
        Assert.Equal("furious", a.Legend!["2"]);
        Assert.Equal(0.5, a.ScoreProbabilities[2]);
    }

    [Fact]
    public async Task Reads_a_noul_answer_which_has_no_confidence()
    {
        var resp = await Parse("{\"answers\":{\"refund\":{\"type\":\"noul\",\"noul\":0.85}}}");
        var a = resp["refund"];

        Assert.Equal(0.85, a.NoulValue);
        Assert.Null(a.Confidence);
        Assert.False(a.IsConfident(0.5));
        Assert.Single(resp.Nouls);
    }

    [Fact]
    public async Task Infers_type_when_the_field_is_absent()
    {
        var resp = await Parse("{\"answers\":{\"x\":{\"choice\":\"a\",\"probabilities\":{\"a\":1.0},\"confidence\":1.0}}}");
        Assert.Equal("choice", resp["x"].Type);
    }

    [Fact]
    public async Task Retries_a_429_then_succeeds()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.TooManyRequests, "{}")
            .Enqueue(HttpStatusCode.OK, "{\"answers\":{}}");
        using var client = ClientWith(handler);

        await client.SystemOneAsync("s", OneNoul);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Unauthorized_and_validation_map_to_typed_errors()
    {
        var auth = new StubHandler().Enqueue(HttpStatusCode.Unauthorized, "{}");
        using (var client = ClientWith(auth))
        {
            await Assert.ThrowsAsync<JevAuthException>(() => client.SystemOneAsync("s", OneNoul));
        }

        var bad = new StubHandler().Enqueue(HttpStatusCode.UnprocessableEntity, "{}");
        using var client2 = ClientWith(bad);
        await Assert.ThrowsAsync<JevValidationException>(() => client2.SystemOneAsync("s", OneNoul));
    }

    [Fact]
    public async Task Exhausted_retries_surface_as_rate_limit()
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.TooManyRequests, "{}");
        using var client = ClientWith(handler, maxRetries: 0);

        await Assert.ThrowsAsync<JevRateLimitException>(() => client.SystemOneAsync("s", OneNoul));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Overloaded_529_is_retryable_then_typed_when_exhausted()
    {
        var handler = new StubHandler().Enqueue((HttpStatusCode)529, "{}");
        using var client = ClientWith(handler, maxRetries: 0);

        await Assert.ThrowsAsync<JevOverloadedException>(() => client.SystemOneAsync("s", OneNoul));
    }
}
