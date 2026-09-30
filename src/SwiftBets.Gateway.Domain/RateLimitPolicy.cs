namespace SwiftBets.Gateway.Domain;

/// <summary>A fixed window: at most <see cref="Limit"/> requests per <see cref="Window"/> for one partition (an IP or a user).</summary>
public sealed record RateLimitPolicy(string Name, int Limit, TimeSpan Window)
{
    public static readonly RateLimitPolicy Login = new("login", 10, TimeSpan.FromMinutes(1));
    public static readonly RateLimitPolicy Placement = new("placement", 120, TimeSpan.FromMinutes(1));
    public static readonly RateLimitPolicy Default = new("default", 600, TimeSpan.FromMinutes(1));

    public static RateLimitPolicy For(string path, string method) =>
        path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) && method == "POST" ? Login
        : path.StartsWith("/api/coupons", StringComparison.OrdinalIgnoreCase) && method == "POST" ? Placement
        : Default;
}
