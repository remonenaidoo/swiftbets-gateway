namespace SwiftBets.Gateway.Domain;

/// <summary>A fixed window: at most <see cref="Limit"/> requests per <see cref="Window"/> for one partition (an IP or a user).</summary>
public sealed record RateLimitPolicy(string Name, int Limit, TimeSpan Window, bool PerUser);
