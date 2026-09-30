using StackExchange.Redis;
using SwiftBets.Gateway.Application.RateLimiting;
using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Infrastructure.RateLimiting;

/// <summary>Fixed-window counters shared by every gateway replica; if Redis is unreachable the request is allowed rather than the edge failing closed.</summary>
public sealed class RedisRateLimitCounter(IConnectionMultiplexer redis) : IRateLimitCounter
{
    private static readonly LuaScript FixedWindow = LuaScript.Prepare(Read("FixedWindow.lua"));

    public async Task<RateLimitDecision> HitAsync(RateLimitPolicy policy, string partition, CancellationToken cancellationToken)
    {
        try
        {
            var result = (RedisResult[])(await redis.GetDatabase().ScriptEvaluateAsync(
                FixedWindow.ExecutableScript, [$"ratelimit:{policy.Name}:{partition}"], [(long)policy.Window.TotalMilliseconds]))!;
            var count = (long)result[0];
            var ttl = TimeSpan.FromMilliseconds(Math.Max(0, (long)result[1]));
            return new RateLimitDecision(count <= policy.Limit, (int)Math.Max(0, policy.Limit - count), ttl);
        }
        catch (RedisException)
        {
            return new RateLimitDecision(true, policy.Limit, TimeSpan.Zero);
        }
    }

    private static string Read(string name)
    {
        using var stream = typeof(RedisRateLimitCounter).Assembly.GetManifestResourceStream($"SwiftBets.Gateway.Infrastructure.RateLimiting.{name}")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
