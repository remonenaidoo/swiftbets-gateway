using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Redis;
using SwiftBets.BuildingBlocks.Resilience;
using SwiftBets.Gateway.Application.RateLimiting;
using SwiftBets.Contracts.Compliance;
using SwiftBets.Contracts.Identity;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.Infrastructure.Compliance;
using SwiftBets.Gateway.Infrastructure.Messaging;
using SwiftBets.Gateway.Infrastructure.RateLimiting;
using SwiftBets.Gateway.Infrastructure.Sessions;

namespace SwiftBets.Gateway.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddGatewayInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSwiftBetsRedis(Required(configuration, "ConnectionStrings:Redis"));
        services.AddFaultInjection(configuration);
        services.AddValidatedOptions<RateLimitOptions>(configuration, RateLimitOptions.SectionName);
        services.AddSingleton<IRateLimitCounter, RedisRateLimitCounter>();
        services.AddValidatedOptions<BrowserSessionOptions>(configuration, BrowserSessionOptions.SectionName);
        services.AddSingleton<ISessionStore, RedisSessionStore>();
        services.AddHttpClient<IIdentityClient, IdentityClient>(http => http.BaseAddress = new Uri(Required(configuration, "Gateway:IdentityAddress").TrimEnd('/') + "/"))
            .AddIdempotentResilience();
        services.AddKafkaMessaging(configuration);
        services.AddKafkaConsumer<SessionRevokedV1, SessionRevokedConsumer>(Topics.SessionRevoked, "gateway.sessions", startAtLatest: true);
        services.AddCompactedState<RestrictionsChangedV1>(Topics.RestrictionsChanged);
        services.AddSingleton<ISessionLimits, CompactedSessionLimits>();
        return services;
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException($"Configuration '{key}' is required.");
}
