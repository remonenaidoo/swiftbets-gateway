using System.Collections.Concurrent;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.TestDoubles;

/// <summary>The session store's semantics without Redis: entries, per-user index and refresh locks. Expiry is not modelled.</summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, SessionEntry> _entries = new();
    private readonly ConcurrentDictionary<string, byte> _locks = new();

    public int Count => _entries.Count(e => e.Value.Session is not null);

    public Task SaveAsync(string sessionHash, BrowserSession session, TimeSpan lifetime)
    {
        _entries[sessionHash] = new SessionEntry(session, null);
        return Task.CompletedTask;
    }

    public Task<SessionEntry?> GetAsync(string sessionHash) => Task.FromResult(_entries.GetValueOrDefault(sessionHash));

    public Task RotateAsync(string oldHash, string newHash, string newSessionId, BrowserSession session, TimeSpan lifetime, TimeSpan grace)
    {
        _entries[newHash] = new SessionEntry(session, null);
        _entries[oldHash] = new SessionEntry(null, newSessionId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<(string Hash, BrowserSession Session)>> ListAsync(string userId) =>
        Task.FromResult<IReadOnlyList<(string, BrowserSession)>>([.. _entries.Where(e => e.Value.Session?.UserId == userId).Select(e => (e.Key, e.Value.Session!))]);

    public Task DeleteAsync(string userId, string sessionHash)
    {
        _entries.TryRemove(sessionHash, out _);
        return Task.CompletedTask;
    }

    public Task DeleteAllAsync(string userId)
    {
        foreach (var entry in _entries.Where(e => e.Value.Session?.UserId == userId))
        {
            _entries.TryRemove(entry.Key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryLockRefreshAsync(string sessionHash, TimeSpan hold) => Task.FromResult(_locks.TryAdd(sessionHash, 0));

    public Task ReleaseRefreshAsync(string sessionHash)
    {
        _locks.TryRemove(sessionHash, out _);
        return Task.CompletedTask;
    }
}
