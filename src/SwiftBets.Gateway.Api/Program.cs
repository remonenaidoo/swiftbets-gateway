using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Gateway.Api.RateLimiting;
using SwiftBets.Gateway.Api.Sessions;
using SwiftBets.Gateway.Application;
using SwiftBets.Gateway.Infrastructure;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-gateway");
builder.Services.AddSwiftBetsWeb();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddGatewayApplication();
builder.Services.AddGatewayInfrastructure(builder.Configuration);
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<BrowserSessionMiddleware>();
app.MapSwiftBetsOperationalEndpoints();
app.MapSessionEndpoints();
app.MapReverseProxy();

await app.RunAsync();
return 0;

public partial class Program;
