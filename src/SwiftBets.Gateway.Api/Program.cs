using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Gateway.Application;
using SwiftBets.Gateway.Infrastructure;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-gateway");
builder.Services.AddSwiftBetsWeb();
builder.Services.AddGatewayApplication();
builder.Services.AddGatewayInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.MapSwiftBetsOperationalEndpoints();
app.MapGet("/", () => Results.Ok(new { service = "swiftbets-gateway" })).ExcludeFromDescription();

await app.RunAsync();
return 0;

public partial class Program;
