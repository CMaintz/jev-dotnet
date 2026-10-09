using System.Net;
using Xunit;

namespace Jev.Client.Tests;

public sealed class ResponseTests
{
    private static readonly Dictionary<string, Question> Questions = new()
    {
        ["team"] = new Choice("Which team", new Dictionary<string, string> { ["billing"] = "pay", ["tech"] = "bugs" }),
        ["anger"] = new Score("How angry", ["calm", "cross", "furious"]),
        ["refund"] = new Noul("Refund?"),
    };

    private static async Task<SystemOneResponse> Parse(string body)
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.OK, body);
        using var client = handler.Client();
        return await client.SystemOneAsync("s", Questions);
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
        var a = resp.GetChoice("team");

        Assert.Equal("jev-1", resp.Model);
        Assert.Equal("tech", a.Choice);
        Assert.Equal(0.9, a.Probabilities["tech"]);
        Assert.True(a.IsConfident(0.7));
        Assert.Equal(10, resp.Usage!.InputTokens);
        Assert.Single(resp.Choices());
    }

    [Fact]
    public async Task Reads_a_score_answer_with_probability_array_and_legend()
    {
        const string body = """
        {"answers":{"anger":{"score":1.5,
        "legend":{"0":"calm","1":"cross","2":"furious"},
        "probabilities":[0.2,0.3,0.5],"confidence":0.6}}}
        """;
        var a = (await Parse(body)).GetScore("anger");

        Assert.Equal(1.5, a.Score);
        Assert.Equal([0.2, 0.3, 0.5], a.Probabilities);
        Assert.Equal("furious", a.Legend["2"]);
        Assert.False(a.IsConfident(0.7));
    }

    [Fact]
    public async Task Reads_a_noul_answer_and_treats_missing_metadata_as_null()
    {
        var resp = await Parse("{\"answers\":{\"refund\":{\"noul\":0.85}}}");

        Assert.Equal(0.85, resp.GetNoul("refund").Probability);
        Assert.True(resp.GetNoul("refund").IsTrue(0.85));
        Assert.Single(resp.Nouls());
        Assert.Empty(resp.Scores());
        Assert.Null(resp.Usage);
        Assert.Null(resp.Model);
    }

    [Fact]
    public async Task Answer_shape_comes_from_the_question_and_unknown_ids_are_ignored()
    {
        var resp = await Parse("{\"answers\":{\"refund\":{\"type\":\"bogus\",\"noul\":0.1},\"extra\":{\"noul\":1}}}");

        Assert.IsType<NoulAnswer>(resp["refund"]);
        Assert.Single(resp.Answers);
    }

    [Fact]
    public async Task Choice_and_score_gate_uniformly_as_calibrated_answers()
    {
        var resp = await Parse(
            "{\"answers\":{\"team\":{\"choice\":\"tech\",\"confidence\":0.9},\"anger\":{\"score\":0.4,\"confidence\":0.3}}}");

        var confident = resp.Answers
            .Where(kv => kv.Value is CalibratedAnswer c && c.IsConfident(0.7))
            .Select(kv => kv.Key);
        Assert.Equal(["team"], confident);
    }

    [Fact]
    public async Task Missing_confidence_is_never_confident()
    {
        var a = (await Parse("{\"answers\":{\"team\":{\"choice\":\"tech\"}}}")).GetChoice("team");

        Assert.Null(a.Confidence);
        Assert.False(a.IsConfident(0));
    }

    [Fact]
    public async Task Answers_and_questions_compare_by_content()
    {
        const string body = "{\"answers\":{\"team\":{\"choice\":\"tech\",\"probabilities\":{\"tech\":0.9},\"confidence\":0.8},"
            + "\"anger\":{\"score\":1,\"probabilities\":[0.5,0.5],\"legend\":{\"0\":\"calm\"}}}}";
        var first = await Parse(body);
        var second = await Parse(body);

        Assert.Equal(first.GetChoice("team"), second.GetChoice("team"));
        Assert.Equal(first, second);
        Assert.Equal(first.GetScore("anger"), second.GetScore("anger"));
        Assert.Equal(Questions["team"], new Choice("Which team", new Dictionary<string, string> { ["tech"] = "bugs", ["billing"] = "pay" }));
        Assert.Equal(Questions["anger"], new Score("How angry", ["calm", "cross", "furious"]));
        Assert.NotEqual(Questions["anger"], new Score("How angry", ["calm", "furious"]));
    }

    [Fact]
    public async Task Huge_token_counts_do_not_overflow()
    {
        var resp = await Parse("{\"answers\":{},\"usage\":{\"input_tokens\":3000000000,\"output_tokens\":7}}");

        Assert.Equal(0, resp.Usage!.InputTokens);
        Assert.Equal(7, resp.Usage.OutputTokens);
    }

    [Fact]
    public async Task Accessors_reject_missing_ids_and_wrong_types()
    {
        var resp = await Parse("{\"answers\":{\"refund\":{\"noul\":0.5}}}");

        Assert.Throws<KeyNotFoundException>(() => resp["team"]);
        Assert.Throws<InvalidOperationException>(() => resp.GetChoice("refund"));
    }

    [Theory]
    [InlineData("<html>oops</html>")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"answers\":[]}")]
    [InlineData("{\"answers\":{\"refund\":{\"noul\":1e400}}}")]
    [InlineData("{\"answers\":{\"refund\":{}}}")]
    [InlineData("{\"answers\":{\"anger\":{\"score\":1,\"probabilities\":[\"x\"]}}}")]
    public async Task Malformed_bodies_surface_as_JevException_with_the_body(string body)
    {
        var error = await Assert.ThrowsAsync<JevException>(() => Parse(body));

        Assert.Equal(body, error.ResponseBody);
        Assert.Null(error.StatusCode);
    }
}
