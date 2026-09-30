using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Redis;
using SwiftBets.BuildingBlocks.Resilience;
using SwiftBets.Gateway.Application.RateLimiting;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.Infrastructure.RateLimiting;
using SwiftBets.Gateway.Infrastructure.Sessions;

namespace SwiftBets.Gateway.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddGatewayInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSwiftBetsRedis(Required(configuration, "ConnectionStrings:Redis"));
        services.AddFaultInjection(configuration);
        services.AddSingleton<IRateLimitCounter, RedisRateLimitCounter>();
        services.AddHttpClient<IIdentityClient, IdentityClient>(http => http.BaseAddress = new Uri(Required(configuration, "Gateway:IdentityAddress").TrimEnd('/') + "/"))
            .AddIdempotentResilience();
        return services;
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException($"Configuration '{key}' is required.");
}
