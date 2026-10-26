using System.Net;
using System.Text.Json;
using Xunit;

namespace Jev.Client.Tests;

public sealed class SystemOneOptionsTests
{
    private const string LongContextHeader = "TypeSafe-Danger-Zone-Long-Context";

    private static readonly Dictionary<string, Question> OneNoul = new() { ["q"] = new Noul("Refund?") };

    [Fact]
    public async Task A_model_override_replaces_the_clients_model_for_that_call_only()
    {
        var handler = new StubHandler();
        using var client = handler.Client();

        await client.SystemOneAsync("s", OneNoul, new SystemOneOptions { Model = "jev-preview" });
        var overridden = JsonDocument.Parse(handler.LastRequestBody!).RootElement.GetProperty("model").GetString();
        await client.SystemOneAsync("s", OneNoul);
        var standard = JsonDocument.Parse(handler.LastRequestBody!).RootElement.GetProperty("model").GetString();

        Assert.Equal("jev-preview", overridden);
        Assert.Equal("jev-latest", standard);
    }

    [Fact]
    public async Task Long_context_sends_its_header_on_that_request_only()
    {
        var handler = new StubHandler();
        using var client = handler.Client();

        await client.SystemOneAsync("s", OneNoul, new SystemOneOptions { LongContext = true });
        var withHeader = handler.LastRequestHeaders.GetValueOrDefault(LongContextHeader);
        await client.SystemOneAsync("s", OneNoul, new SystemOneOptions { Model = "jev-preview" });

        Assert.Equal("true", withHeader);
        Assert.False(handler.LastRequestHeaders.ContainsKey(LongContextHeader));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task A_blank_model_override_is_rejected_before_sending(string model)
    {
        var handler = new StubHandler();
        using var client = handler.Client();

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SystemOneAsync("s", OneNoul, new SystemOneOptions { Model = model }));
        Assert.Equal(0, handler.Calls);
    }
}
