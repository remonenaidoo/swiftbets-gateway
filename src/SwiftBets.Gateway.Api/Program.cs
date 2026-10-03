using Microsoft.AspNetCore.HttpOverrides;
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
// The TLS edge is a private-network hop; take its scheme so proxied services see https, and the client address it saw
// (one hop only) so rate limits count per visitor rather than per edge. A public peer's X-Forwarded-For is ignored.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("127.0.0.0/8"));
});
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.UseForwardedHeaders();
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
