using System.Text.Json;
using Xunit;

namespace Jev.Client.Tests;

public sealed class CompressionTests
{
    private static readonly Dictionary<string, Question> OneNoul = new() { ["q"] = new Noul("Refund?") };

    private static readonly string LongState = string.Concat(Enumerable.Repeat("The customer was charged twice. ", 64));

    private static JevClient Client(StubHandler handler, bool compress) => new(new JevClientOptions
    {
        ApiKey = "test-key",
        Handler = handler,
        CompressRequests = compress,
    });

    [Fact]
    public async Task A_body_of_1024_bytes_or_more_is_sent_gzipped()
    {
        var handler = new StubHandler();
        using var client = handler.Client();

        await client.SystemOneAsync(LongState, OneNoul);

        Assert.Equal("gzip", handler.LastRequestHeaders.GetValueOrDefault("Content-Encoding"));
        Assert.Equal([0x1F, 0x8B], handler.LastRequestBytes!.Take(2));
        Assert.True(handler.LastRequestBytes!.Length < LongState.Length);
        Assert.Equal(LongState, JsonDocument.Parse(handler.LastRequestBody!).RootElement.GetProperty("state").GetString());
    }

    [Theory]
    [InlineData(1023, null)]
    [InlineData(1024, "gzip")]
    public async Task Compression_starts_at_exactly_1024_bytes(int bodyBytes, string? expected)
    {
        var measure = new StubHandler();
        using (var plain = Client(measure, compress: false))
        {
            await plain.SystemOneAsync("", OneNoul);
        }

        var handler = new StubHandler();
        using var client = handler.Client();
        await client.SystemOneAsync(new string('x', bodyBytes - measure.LastRequestBytes!.Length), OneNoul);

        Assert.Equal(bodyBytes, System.Text.Encoding.UTF8.GetByteCount(handler.LastRequestBody!));
        Assert.Equal(expected, handler.LastRequestHeaders.GetValueOrDefault("Content-Encoding"));
    }

    [Fact]
    public async Task Turning_compression_off_sends_even_a_large_body_plain()
    {
        var handler = new StubHandler();
        using var client = Client(handler, compress: false);

        await client.SystemOneAsync(LongState, OneNoul);

        Assert.False(handler.LastRequestHeaders.ContainsKey("Content-Encoding"));
        Assert.Equal(LongState, JsonDocument.Parse(handler.LastRequestBytes).RootElement.GetProperty("state").GetString());
    }
}
