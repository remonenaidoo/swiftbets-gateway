namespace SwiftBets.Gateway.Domain;

/// <summary>Picks the bucket for a request. Calls that send an email (registration, reset, verification resend) get their own, tighter one.</summary>
public sealed record RateLimitPolicies(RateLimitPolicy Login, RateLimitPolicy AccountEmail, RateLimitPolicy Placement, RateLimitPolicy Default)
{
    private static readonly string[] EmailPaths = ["/api/auth/register", "/api/auth/password-reset", "/api/auth/verify-email/resend"];

    public RateLimitPolicy For(string path, string method) =>
        method == "POST" && EmailPaths.Any(p => string.Equals(path.TrimEnd('/'), p, StringComparison.OrdinalIgnoreCase)) ? AccountEmail
        : method == "POST" && (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/session/", StringComparison.OrdinalIgnoreCase)) ? Login
        : method == "POST" && path.StartsWith("/api/coupons", StringComparison.OrdinalIgnoreCase) ? Placement
        : Default;
}
