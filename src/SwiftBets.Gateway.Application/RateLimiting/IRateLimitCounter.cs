using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Application.RateLimiting;

public interface IRateLimitCounter
{
    Task<RateLimitDecision> HitAsync(RateLimitPolicy policy, string partition, CancellationToken cancellationToken);
}
