using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Application.Sessions;

/// <summary>Browser sessions keyed by the hash of their cookie value, indexed per user so they can be listed and revoked.</summary>
public interface ISessionStore
{
    Task SaveAsync(string sessionHash, BrowserSession session, TimeSpan lifetime);

    Task<SessionEntry?> GetAsync(string sessionHash);

    /// <summary>
    /// Replaces a session under a new cookie value. The old entry points at the new value for <paramref name="grace"/>,
    /// so requests already in flight with the old cookie follow it instead of being signed out.
    /// </summary>
    Task RotateAsync(string oldHash, string newHash, string newSessionId, BrowserSession session, TimeSpan lifetime, TimeSpan grace);

    Task<IReadOnlyList<(string Hash, BrowserSession Session)>> ListAsync(string userId);

    Task DeleteAsync(string userId, string sessionHash);

    Task DeleteAllAsync(string userId);

    /// <summary>Claims the right to refresh one session; only the holder calls identity, so a single-use refresh token is used once.</summary>
    Task<bool> TryLockRefreshAsync(string sessionHash, TimeSpan hold);

    Task ReleaseRefreshAsync(string sessionHash);
}
