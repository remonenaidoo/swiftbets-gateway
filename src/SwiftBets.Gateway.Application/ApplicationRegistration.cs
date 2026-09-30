using Microsoft.Extensions.DependencyInjection;

namespace SwiftBets.Gateway.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddGatewayApplication(this IServiceCollection services) => services;
}
