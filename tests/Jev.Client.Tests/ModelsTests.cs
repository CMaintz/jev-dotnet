using System.Net;
using Xunit;

namespace Jev.Client.Tests;

public sealed class ModelsTests
{
    private const string TwoModels = """
        {"models":[
        {"name":"jev-latest","description":"General-purpose system one model.","release_date":"2026-09-15"},
        {"name":"jev-1.13.0","description":"Pinned.","release_date":"2026-05-02"}]}
        """;

    [Fact]
    public async Task Lists_models_with_a_GET_that_keeps_the_base_url_path()
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.OK, TwoModels);
        using var client = new JevClient(new JevClientOptions
        {
            ApiKey = "test-key",
            Handler = handler,
            BaseUrl = new Uri("https://gateway.example.com/typesafe/"),
        });

        var models = await client.ModelsAsync();

        Assert.Equal(
            [
                new ModelInfo("jev-latest", "General-purpose system one model.", new DateOnly(2026, 9, 15)),
                new ModelInfo("jev-1.13.0", "Pinned.", new DateOnly(2026, 5, 2)),
            ],
            models);
        Assert.Equal(HttpMethod.Get, handler.LastRequestMethod);
        Assert.Equal(new Uri("https://gateway.example.com/typesafe/v1/models"), handler.LastRequestUri);
        Assert.Equal("Bearer test-key", handler.LastAuthorization);
        Assert.Null(handler.LastRequestBody);
    }

    [Fact]
    public async Task Models_are_retried_and_mapped_to_typed_errors_like_system_one()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.ServiceUnavailable, "{}")
            .Enqueue(HttpStatusCode.OK, TwoModels)
            .Enqueue(HttpStatusCode.Unauthorized, "{\"detail\":\"bad key\"}");
        using var client = handler.Client();

        Assert.Equal(2, (await client.ModelsAsync()).Count);
        Assert.Equal(2, handler.Calls);
        var error = await Assert.ThrowsAsync<JevAuthException>(() => client.ModelsAsync());
        Assert.Equal("{\"detail\":\"bad key\"}", error.ResponseBody);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"models\":{}}")]
    [InlineData("{\"models\":[\"jev-latest\"]}")]
    [InlineData("{\"models\":[{\"description\":\"d\",\"release_date\":\"2026-09-15\"}]}")]
    [InlineData("{\"models\":[{\"name\":\"m\",\"description\":7,\"release_date\":\"2026-09-15\"}]}")]
    [InlineData("{\"models\":[{\"name\":\"m\",\"description\":\"d\"}]}")]
    [InlineData("{\"models\":[{\"name\":\"m\",\"description\":\"d\",\"release_date\":\"15/09/2026\"}]}")]
    [InlineData("{\"models\":[{\"name\":\"m\",\"description\":\"d\",\"release_date\":\"2026-02-30\"}]}")]
    public async Task Malformed_model_lists_surface_as_JevException_with_the_body(string body)
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.OK, body);
        using var client = handler.Client();

        var error = await Assert.ThrowsAsync<JevException>(() => client.ModelsAsync());

        Assert.Equal(body, error.ResponseBody);
        Assert.Null(error.StatusCode);
    }
}
