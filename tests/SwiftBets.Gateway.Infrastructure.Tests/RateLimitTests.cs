using StackExchange.Redis;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Gateway.Domain;
using SwiftBets.Gateway.Infrastructure.RateLimiting;

[assembly: AssemblyFixture(typeof(RedisFixture))]

namespace SwiftBets.Gateway.Infrastructure.Tests;

public sealed class RateLimitTests(RedisFixture redis)
{
    private static readonly RateLimitPolicy Tight = new("test", 3, TimeSpan.FromMinutes(1), PerUser: false);

    [Fact]
    public async Task Requests_within_the_window_limit_are_allowed()
    {
        var counter = new RedisRateLimitCounter(await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString));

        var decisions = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => counter.HitAsync(Tight, "10.0.0.1", CancellationToken.None)));

        decisions.ShouldAllBe(d => d.Allowed);
    }

    [Fact]
    public async Task Request_over_the_limit_is_refused_with_a_retry_after()
    {
        var counter = new RedisRateLimitCounter(await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString));
        for (var i = 0; i < 3; i++)
        {
            await counter.HitAsync(Tight, "10.0.0.2", CancellationToken.None);
        }

        var decision = await counter.HitAsync(Tight, "10.0.0.2", CancellationToken.None);

        decision.Allowed.ShouldBeFalse();
        decision.RetryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
    }
}
