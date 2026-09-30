namespace SwiftBets.Gateway.Domain;

public sealed record RateLimitDecision(bool Allowed, int Remaining, TimeSpan RetryAfter);
