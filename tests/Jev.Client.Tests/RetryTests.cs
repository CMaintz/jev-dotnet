using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Jev.Client.Tests;

public sealed class RetryTests
{
    private static readonly Dictionary<string, Question> OneNoul = new() { ["q"] = new Noul("Refund?") };

    [Fact]
    public async Task Retries_429_and_529_with_growing_backoff_then_succeeds()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.TooManyRequests, "{}")
            .Enqueue((HttpStatusCode)529, "{}")
            .Enqueue(HttpStatusCode.OK, "{\"answers\":{\"q\":{\"noul\":0.9}}}");
        using var client = handler.Client();

        var response = await client.SystemOneAsync("s", OneNoul);

        Assert.Equal(0.9, response.GetNoul("q").Probability);
        Assert.Equal(3, handler.Calls);
        Assert.Equal(2, handler.Waits.Count);
        Assert.True(handler.Waits[1] >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Retry_after_seconds_overrides_the_computed_delay()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.TooManyRequests, "{}", new RetryConditionHeaderValue(TimeSpan.FromSeconds(4)));
        using var client = handler.Client(maxRetries: 1);

        await client.SystemOneAsync("s", OneNoul);

        Assert.InRange(handler.Waits[0], TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(4250));
    }

    [Fact]
    public async Task Retry_after_dates_and_long_delays_are_handled_and_capped()
    {
        var handler = new StubHandler();
        handler
            .Enqueue(HttpStatusCode.TooManyRequests, "{}", new RetryConditionHeaderValue(handler.Time.Now.AddSeconds(2)))
            .Enqueue(HttpStatusCode.TooManyRequests, "{}", new RetryConditionHeaderValue(TimeSpan.FromHours(1)));
        using var client = handler.Client(maxRetries: 2);

        await client.SystemOneAsync("s", OneNoul);

        Assert.InRange(handler.Waits[0], TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(2250));
        Assert.Equal(TimeSpan.FromSeconds(30), handler.Waits[1]);
    }

    [Fact]
    public async Task A_body_with_a_byte_order_mark_is_read()
    {
        var handler = new StubHandler().EnqueueWithByteOrderMark("{\"answers\":{\"q\":{\"noul\":0.8}}}");
        using var client = handler.Client();

        var response = await client.SystemOneAsync("s", OneNoul);

        Assert.Equal(0.8, response.GetNoul("q").Probability);
    }

    [Fact]
    public async Task A_body_with_a_bogus_charset_is_still_read()
    {
        var handler = new StubHandler().EnqueueWithBogusCharset(HttpStatusCode.BadGateway, "bad gateway");
        using var client = handler.Client(maxRetries: 0);

        var error = await Assert.ThrowsAsync<JevException>(() => client.SystemOneAsync("s", OneNoul));

        Assert.Equal(502, error.StatusCode);
        Assert.Equal("bad gateway", error.ResponseBody);
    }

    [Fact]
    public async Task Exhausted_retries_surface_as_typed_errors_with_the_last_body()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.TooManyRequests, "{}")
            .Enqueue(HttpStatusCode.TooManyRequests, "{\"error\":\"slow down\"}");
        using var client = handler.Client(maxRetries: 1);

        var error = await Assert.ThrowsAsync<JevRateLimitException>(() => client.SystemOneAsync("s", OneNoul));

        Assert.Equal(2, handler.Calls);
        Assert.Equal("{\"error\":\"slow down\"}", error.ResponseBody);

        using var overloaded = new StubHandler().Enqueue((HttpStatusCode)529, "{}").Client(maxRetries: 0);
        await Assert.ThrowsAsync<JevOverloadedException>(() => overloaded.SystemOneAsync("s", OneNoul));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, typeof(JevAuthException))]
    [InlineData(HttpStatusCode.UnprocessableEntity, typeof(JevValidationException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(JevException))]
    public async Task Non_retryable_statuses_fail_immediately_with_their_code(HttpStatusCode status, Type expected)
    {
        var handler = new StubHandler().Enqueue(status, "boom");
        using var client = handler.Client();

        var error = await Assert.ThrowsAsync(expected, () => client.SystemOneAsync("s", OneNoul));

        Assert.Equal(1, handler.Calls);
        Assert.Equal((int)status, ((JevException)error).StatusCode);
        Assert.Equal("boom", ((JevException)error).ResponseBody);
    }

    [Fact]
    public async Task Network_failures_and_timeouts_become_JevException()
    {
        using var network = new StubHandler().FailWith(new HttpRequestException("reset")).Client();
        var error = await Assert.ThrowsAsync<JevException>(() => network.SystemOneAsync("s", OneNoul));
        Assert.IsType<HttpRequestException>(error.InnerException);

        using var timeout = new StubHandler().FailWith(new TaskCanceledException("timed out")).Client();
        await Assert.ThrowsAsync<JevException>(() => timeout.SystemOneAsync("s", OneNoul));
    }

    [Fact]
    public async Task Caller_cancellation_stays_an_OperationCanceledException()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.TooManyRequests, "{}", new RetryConditionHeaderValue(TimeSpan.FromMinutes(1)));
        using var client = new JevClient(new JevClientOptions { ApiKey = "k", Handler = handler });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SystemOneAsync("s", OneNoul, cts.Token));
        Assert.Equal(1, handler.Calls);
    }
}
