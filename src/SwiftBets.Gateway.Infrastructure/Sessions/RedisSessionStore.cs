using System.Text.Json;
using StackExchange.Redis;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Infrastructure.Sessions;

/// <summary>
/// <c>gateway:session:{hash}</c> holds a session (or, briefly after rotation, a pointer to its replacement) and
/// <c>gateway:user-sessions:{userId}</c> indexes a user's session hashes for device lists and revocation.
/// </summary>
public sealed class RedisSessionStore(IConnectionMultiplexer redis) : ISessionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private IDatabase Db => redis.GetDatabase();

    public async Task SaveAsync(string sessionHash, BrowserSession session, TimeSpan lifetime)
    {
        var transaction = Db.CreateTransaction();
        _ = transaction.StringSetAsync(SessionKey(sessionHash), JsonSerializer.Serialize(new StoredEntry(session, null), Json), lifetime);
        _ = transaction.SetAddAsync(UserKey(session.UserId), sessionHash);
        _ = transaction.KeyExpireAsync(UserKey(session.UserId), lifetime);
        await transaction.ExecuteAsync();
    }

    public async Task<SessionEntry?> GetAsync(string sessionHash) =>
        await Db.StringGetAsync(SessionKey(sessionHash)) is { HasValue: true } value
            ? JsonSerializer.Deserialize<StoredEntry>(value.ToString(), Json)!.ToEntry()
            : null;

    public async Task RotateAsync(string oldHash, string newHash, string newSessionId, BrowserSession session, TimeSpan lifetime, TimeSpan grace)
    {
        var transaction = Db.CreateTransaction();
        _ = transaction.StringSetAsync(SessionKey(newHash), JsonSerializer.Serialize(new StoredEntry(session, null), Json), lifetime);
        _ = transaction.StringSetAsync(SessionKey(oldHash), JsonSerializer.Serialize(new StoredEntry(null, newSessionId), Json), grace);
        _ = transaction.SetRemoveAsync(UserKey(session.UserId), oldHash);
        _ = transaction.SetAddAsync(UserKey(session.UserId), newHash);
        _ = transaction.KeyExpireAsync(UserKey(session.UserId), lifetime);
        await transaction.ExecuteAsync();
    }

    public async Task<IReadOnlyList<(string Hash, BrowserSession Session)>> ListAsync(string userId)
    {
        var hashes = (await Db.SetMembersAsync(UserKey(userId))).Select(h => h.ToString()).ToArray();
        if (hashes.Length == 0)
        {
            return [];
        }

        var values = await Db.StringGetAsync([.. hashes.Select(h => (RedisKey)SessionKey(h))]);
        var live = new List<(string, BrowserSession)>();
        var gone = new List<RedisValue>();
        for (var i = 0; i < hashes.Length; i++)
        {
            if (values[i].HasValue && JsonSerializer.Deserialize<StoredEntry>(values[i].ToString(), Json)!.Session is { } session)
            {
                live.Add((hashes[i], session));
            }
            else
            {
                gone.Add(hashes[i]);
            }
        }

        if (gone.Count > 0)
        {
            await Db.SetRemoveAsync(UserKey(userId), [.. gone]);
        }

        return [.. live.OrderBy(s => s.Item2.CreatedAt)];
    }

    public async Task DeleteAsync(string userId, string sessionHash)
    {
        var transaction = Db.CreateTransaction();
        _ = transaction.KeyDeleteAsync(SessionKey(sessionHash));
        _ = transaction.SetRemoveAsync(UserKey(userId), sessionHash);
        await transaction.ExecuteAsync();
    }

    public async Task DeleteAllAsync(string userId)
    {
        var hashes = await Db.SetMembersAsync(UserKey(userId));
        await Db.KeyDeleteAsync([.. hashes.Select(h => (RedisKey)SessionKey(h.ToString())), UserKey(userId)]);
    }

    public Task<bool> TryLockRefreshAsync(string sessionHash, TimeSpan hold) =>
        Db.StringSetAsync(LockKey(sessionHash), "1", hold, When.NotExists);

    public Task ReleaseRefreshAsync(string sessionHash) => Db.KeyDeleteAsync(LockKey(sessionHash));

    private static string SessionKey(string hash) => $"gateway:session:{hash}";

    private static string UserKey(string userId) => $"gateway:user-sessions:{userId}";

    private static string LockKey(string hash) => $"gateway:session-refresh:{hash}";

    private sealed record StoredEntry(BrowserSession? Session, string? RotatedTo)
    {
        public SessionEntry ToEntry() => new(Session, RotatedTo);
    }
}
