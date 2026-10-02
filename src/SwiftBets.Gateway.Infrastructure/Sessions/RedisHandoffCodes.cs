using System.Text.Json;
using SwiftBets.Gateway.Application.Sessions;
using StackExchange.Redis;

namespace SwiftBets.Gateway.Infrastructure.Sessions;

public sealed class RedisHandoffCodes(IConnectionMultiplexer redis) : IHandoffCodes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task StoreAsync(string codeHash, SessionTokens browser, TimeSpan lifetime) =>
        redis.GetDatabase().StringSetAsync(Key(codeHash), JsonSerializer.Serialize(browser, Json), lifetime);

    public async Task<SessionTokens?> TakeAsync(string codeHash) =>
        await redis.GetDatabase().StringGetDeleteAsync(Key(codeHash)) is { HasValue: true } value
            ? JsonSerializer.Deserialize<SessionTokens>(value.ToString(), Json)
            : null;

    private static string Key(string codeHash) => $"gateway:handoff:{codeHash}";
}
