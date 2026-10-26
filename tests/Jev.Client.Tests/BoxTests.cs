using System.Net;
using System.Text.Json;
using Xunit;

namespace Jev.Client.Tests;

public sealed class BoxTests
{
    private const string ImagesHeader = "TypeSafe-Danger-Zone-Images";

    private static readonly Dictionary<string, Question> FindMug = new() { ["mug"] = new Box("the coffee mug in `photo`") };

    private static async Task<SystemOneResponse> Parse(string body)
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.OK, body);
        using var client = handler.Client();
        return await client.SystemOneAsync("s", FindMug);
    }

    [Fact]
    public async Task A_box_is_sent_without_criteria_and_turns_on_images()
    {
        var handler = new StubHandler();
        using var client = handler.Client();

        await client.SystemOneAsync("s", new Dictionary<string, Question> { ["q"] = new Noul("Refund?") });
        var plainHeaders = handler.LastRequestHeaders;
        await client.SystemOneAsync("s", FindMug);
        var mug = JsonDocument.Parse(handler.LastRequestBody!).RootElement.GetProperty("questions").GetProperty("mug");

        Assert.Equal("box", mug.GetProperty("type").GetString());
        Assert.Equal("the coffee mug in `photo`", mug.GetProperty("instructions").GetString());
        Assert.False(mug.TryGetProperty("criteria", out _));
        Assert.Equal("true", handler.LastRequestHeaders.GetValueOrDefault(ImagesHeader));
        Assert.False(plainHeaders.ContainsKey(ImagesHeader));
    }

    [Fact]
    public async Task Reads_each_edge_and_the_confidence()
    {
        var response = await Parse("""
            {"answers":{"mug":{"type":"box","box":{"top":0.25,"bottom":0.82,"left":0.44,"right":0.84},"confidence":0.92}}}
            """);
        var mug = response.GetBox("mug");

        Assert.Equal(new BoxAnswer(Top: 0.25, Bottom: 0.82, Left: 0.44, Right: 0.84, Confidence: 0.92), mug);
        Assert.True(mug.IsConfident(0.92));
        Assert.False(mug.IsConfident(0.93));
        Assert.Equal(["mug"], response.Boxes().Keys);
    }

    [Fact]
    public async Task A_null_confidence_is_never_confident()
    {
        var mug = (await Parse("""
            {"answers":{"mug":{"box":{"top":0.1,"bottom":0.2,"left":0.3,"right":0.4},"confidence":null}}}
            """)).GetBox("mug");

        Assert.Null(mug.Confidence);
        Assert.False(mug.IsConfident(0));
    }

    [Theory]
    [InlineData("{\"answers\":{\"mug\":{\"confidence\":0.9}}}")]
    [InlineData("{\"answers\":{\"mug\":{\"box\":[0.1,0.2,0.3,0.4]}}}")]
    [InlineData("{\"answers\":{\"mug\":{\"box\":{\"top\":0.1,\"bottom\":0.2,\"left\":0.3}}}}")]
    [InlineData("{\"answers\":{\"mug\":{\"box\":{\"top\":0.1,\"bottom\":\"0.2\",\"left\":0.3,\"right\":0.4}}}}")]
    [InlineData("{\"answers\":{\"mug\":{\"box\":{\"top\":0.1,\"bottom\":1e400,\"left\":0.3,\"right\":0.4}}}}")]
    public async Task A_missing_or_invalid_box_is_a_malformed_response(string body)
    {
        var error = await Assert.ThrowsAsync<JevException>(() => Parse(body));

        Assert.Equal(body, error.ResponseBody);
    }
}
