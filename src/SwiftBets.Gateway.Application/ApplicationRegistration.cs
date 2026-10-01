using Microsoft.Extensions.DependencyInjection;
using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddGatewayApplication(this IServiceCollection services)
    {
        services.AddScoped<BrowserSessions>();
        return services;
    }
}
