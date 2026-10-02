using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.TestDoubles;

namespace SwiftBets.Gateway.Api.Tests;

public sealed class HostTests : IClassFixture<HostTests.Factory>
{
    private readonly HttpClient _client;

    public HostTests(Factory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_is_healthy_without_dependencies()
    {
        using var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_reports_unavailable_dependencies()
    {
        using var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Unreachable_upstream_returns_bad_gateway()
    {
        using var response = await _client.GetAsync(new Uri("/api/fixtures/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task The_catalogue_is_routed_to_offer()
    {
        using var response = await _client.GetAsync(new Uri("/api/catalog/sports", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Metrics_are_exposed()
    {
        var body = await _client.GetStringAsync(new Uri("/metrics", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("process_cpu_seconds_total");
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Redis", "127.0.0.1:1,connectTimeout=200");
            builder.UseSetting("Gateway:IdentityAddress", "http://127.0.0.1:1");
            builder.UseSetting("Kafka:BootstrapServers", "127.0.0.1:9");
            builder.UseSetting("Kafka:Environment", "test");
            builder.UseSetting("Kafka:ClientId", "gateway-tests");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ISessionStore, InMemorySessionStore>();
                services.AddSingleton<ISessionLimits>(FixedSessionLimits.None);
                services.AddSingleton<IIdentityClient>(new FakeIdentity(TimeProvider.System, "operator-1", "Operator"));
            });
        }
    }
}
