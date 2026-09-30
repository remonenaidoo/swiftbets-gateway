using System.ComponentModel.DataAnnotations;
using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Application.RateLimiting;

public sealed class RateLimitOptions
{
    public const string SectionName = "Gateway:RateLimits";

    [Range(1, 1_000_000)]
    public int LoginPerMinute { get; set; } = 10;

    [Range(1, 1_000_000)]
    public int PlacementsPerUserPerMinute { get; set; } = 120;

    [Range(1, 10_000_000)]
    public int RequestsPerIpPerMinute { get; set; } = 600;

    public RateLimitPolicies ToPolicies() => new(
        new RateLimitPolicy("login", LoginPerMinute, TimeSpan.FromMinutes(1), PerUser: false),
        new RateLimitPolicy("placement", PlacementsPerUserPerMinute, TimeSpan.FromMinutes(1), PerUser: true),
        new RateLimitPolicy("default", RequestsPerIpPerMinute, TimeSpan.FromMinutes(1), PerUser: false));
}
