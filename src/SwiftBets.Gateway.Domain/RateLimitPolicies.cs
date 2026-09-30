namespace SwiftBets.Gateway.Domain;

public sealed record RateLimitPolicies(RateLimitPolicy Login, RateLimitPolicy Placement, RateLimitPolicy Default)
{
    public RateLimitPolicy For(string path, string method) =>
        method == "POST" && (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/session/", StringComparison.OrdinalIgnoreCase)) ? Login
        : method == "POST" && path.StartsWith("/api/coupons", StringComparison.OrdinalIgnoreCase) ? Placement
        : Default;
}
