using Microsoft.Extensions.Options;
using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Application.Sessions;

/// <summary>
/// Opaque browser sessions: the browser holds only a random id, the tokens stay here. Refresh rotates the id and is
/// single-flight per session, because identity's refresh tokens are single-use and a second use revokes the family.
/// </summary>
public sealed class BrowserSessions(ISessionStore store, IIdentityClient identity, IOptions<BrowserSessionOptions> options, TimeProvider time)
{
    private static readonly TimeSpan LockHold = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FollowerWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan FollowerPoll = TimeSpan.FromMilliseconds(100);

    private TimeSpan Lifetime => TimeSpan.FromDays(options.Value.LifetimeDays);

    private TimeSpan Grace => TimeSpan.FromSeconds(options.Value.RotationGraceSeconds);

    public async Task<string> StartAsync(SessionTokens tokens, string device)
    {
        var now = time.GetUtcNow();
        var sessionId = SessionId.New();
        var session = new BrowserSession(Guid.CreateVersion7(), Subject(tokens.AccessToken), JwtClaims.ReadStrings(tokens.AccessToken, "role"),
            tokens.AccessToken, tokens.RefreshToken, Expiry(tokens), now, now, device);
        await store.SaveAsync(SessionId.Hash(sessionId), session, Lifetime);
        return sessionId;
    }

    /// <summary>Null when the cookie names no live session (unknown, revoked, or refresh refused by identity).</summary>
    public async Task<SessionResolution?> ResolveAsync(string? sessionId, CancellationToken cancellationToken)
    {
        if (!SessionId.IsWellFormed(sessionId))
        {
            return null;
        }

        var (session, hash, rotatedTo) = await LoadAsync(sessionId!);
        if (session is null)
        {
            return null;
        }

        var now = time.GetUtcNow();
        if (session.NeedsRefresh(now, TimeSpan.FromSeconds(options.Value.RefreshAheadSeconds)))
        {
            return await RefreshAsync(session, hash, cancellationToken);
        }

        if (now - session.LastSeenAt > TimeSpan.FromSeconds(options.Value.LastSeenResolutionSeconds))
        {
            session = session with { LastSeenAt = now };
            await store.SaveAsync(hash, session, Lifetime);
        }

        return new SessionResolution(session, hash, rotatedTo);
    }

    public async Task<IReadOnlyList<(string Hash, BrowserSession Session)>> ListAsync(string userId) => await store.ListAsync(userId);

    public async Task<bool> RevokeAsync(string userId, Guid publicId)
    {
        var match = (await store.ListAsync(userId)).FirstOrDefault(s => s.Session.PublicId == publicId);
        if (match.Session is null)
        {
            return false;
        }

        await store.DeleteAsync(userId, match.Hash);
        await identity.RevokeAsync(match.Session.RefreshToken, CancellationToken.None);
        return true;
    }

    public async Task RevokeAllAsync(string userId)
    {
        var sessions = await store.ListAsync(userId);
        await store.DeleteAllAsync(userId);
        foreach (var (_, session) in sessions)
        {
            await identity.RevokeAsync(session.RefreshToken, CancellationToken.None);
        }
    }

    /// <summary>Drops every browser session of a user whose refresh tokens identity has already revoked (suspension, exclusion).</summary>
    public async Task<int> DropAllAsync(string userId)
    {
        var sessions = await store.ListAsync(userId);
        await store.DeleteAllAsync(userId);
        return sessions.Count;
    }

    public async Task EndAsync(SessionResolution resolution)
    {
        await store.DeleteAsync(resolution.Session.UserId, resolution.SessionHash);
        await identity.RevokeAsync(resolution.Session.RefreshToken, CancellationToken.None);
    }

    private async Task<SessionResolution?> RefreshAsync(BrowserSession session, string hash, CancellationToken cancellationToken)
    {
        if (!await store.TryLockRefreshAsync(hash, LockHold))
        {
            return await FollowRotationAsync(session, hash, cancellationToken);
        }

        try
        {
            if ((await store.GetAsync(hash))?.Session is not { } current)
            {
                return await FollowRotationAsync(session, hash, cancellationToken);
            }

            var tokens = await identity.RefreshAsync(current.RefreshToken, cancellationToken);
            if (tokens is null)
            {
                await store.DeleteAsync(current.UserId, hash);
                return null;
            }

            var now = time.GetUtcNow();
            var renewed = current with
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                AccessExpiresAt = Expiry(tokens),
                Roles = JwtClaims.ReadStrings(tokens.AccessToken, "role"),
                LastSeenAt = now,
            };
            var newId = SessionId.New();
            var newHash = SessionId.Hash(newId);
            await store.RotateAsync(hash, newHash, newId, renewed, Lifetime, Grace);
            return new SessionResolution(renewed, newHash, newId);
        }
        finally
        {
            await store.ReleaseRefreshAsync(hash);
        }
    }

    /// <summary>Another request is refreshing this session: wait for its rotation and use the result.</summary>
    private async Task<SessionResolution?> FollowRotationAsync(BrowserSession stale, string hash, CancellationToken cancellationToken)
    {
        var deadline = time.GetUtcNow() + FollowerWait;
        while (time.GetUtcNow() < deadline)
        {
            var entry = await store.GetAsync(hash);
            if (entry is null)
            {
                return null;
            }

            if (entry.RotatedTo is { } next && (await store.GetAsync(SessionId.Hash(next)))?.Session is { } rotated)
            {
                return new SessionResolution(rotated, SessionId.Hash(next), next);
            }

            await Task.Delay(FollowerPoll, time, cancellationToken);
        }

        return stale.AccessExpiresAt > time.GetUtcNow() ? new SessionResolution(stale, hash, null) : null;
    }

    private async Task<(BrowserSession? Session, string Hash, string? RotatedTo)> LoadAsync(string sessionId)
    {
        var hash = SessionId.Hash(sessionId);
        var entry = await store.GetAsync(hash);
        if (entry?.RotatedTo is { } next)
        {
            var nextHash = SessionId.Hash(next);
            return ((await store.GetAsync(nextHash))?.Session, nextHash, next);
        }

        return (entry?.Session, hash, null);
    }

    private DateTimeOffset Expiry(SessionTokens tokens) =>
        JwtClaims.ReadExpiry(tokens.AccessToken) ?? time.GetUtcNow().AddSeconds(tokens.ExpiresIn);

    private static string Subject(string accessToken) =>
        JwtClaims.ReadString(accessToken, "sub") ?? throw new InvalidOperationException("Identity issued a token without a subject.");
}
