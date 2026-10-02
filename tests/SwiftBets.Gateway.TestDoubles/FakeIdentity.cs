using System.Text;
using System.Text.Json;
using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.TestDoubles;

/// <summary>Issues unsigned JWT-shaped tokens and treats refresh tokens as single-use, like identity.</summary>
public sealed class FakeIdentity(TimeProvider time, string subject = "punter-1", string role = "Punter") : IIdentityClient
{
    private readonly HashSet<string> _issuedRefreshTokens = [];
    private readonly Lock _gate = new();
    private int _refreshCalls;

    public int RefreshCalls => _refreshCalls;

    public TimeSpan AccessLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan RefreshDelay { get; set; } = TimeSpan.Zero;

    public bool RefuseRefresh { get; set; }

    public Task<SessionTokens?> SignInAsync(string username, string password, CancellationToken cancellationToken) =>
        Task.FromResult<SessionTokens?>(password == "right" ? Issue() : null);

    public async Task<SessionTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _refreshCalls);
        if (RefreshDelay > TimeSpan.Zero)
        {
            await Task.Delay(RefreshDelay, cancellationToken);
        }

        lock (_gate)
        {
            return _issuedRefreshTokens.Remove(refreshToken) && !RefuseRefresh ? Issue() : null;
        }
    }

    public async Task<SessionHandoff?> HandoffAsync(string refreshToken, CancellationToken cancellationToken) =>
        await RefreshAsync(refreshToken, cancellationToken) is { } device ? new SessionHandoff(device, Issue()) : null;

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _issuedRefreshTokens.Remove(refreshToken);
            Revoked.Add(refreshToken);
        }

        return Task.CompletedTask;
    }

    public List<string> Revoked { get; } = [];

    public SessionTokens Issue()
    {
        var refresh = Guid.NewGuid().ToString("N");
        lock (_gate)
        {
            _issuedRefreshTokens.Add(refresh);
        }

        return new SessionTokens(Token(subject, role, time.GetUtcNow() + AccessLifetime), refresh, (int)AccessLifetime.TotalSeconds);
    }

    public static string Token(string subject, string role, DateTimeOffset expires)
    {
        static string Part(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Part(new { alg = "RS256" })}.{Part(new { sub = subject, role, exp = expires.ToUnixTimeSeconds(), jti = Guid.NewGuid() })}.signature";
    }
}
