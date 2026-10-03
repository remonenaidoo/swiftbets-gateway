using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.Gateway.Application.RateLimiting;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.Domain;
using SwiftBets.Gateway.TestDoubles;

namespace SwiftBets.Gateway.Api.Tests;

public sealed class RateLimitPartitionTests : IClassFixture<RateLimitPartitionTests.Factory>
{
    private readonly Factory _factory;

    public RateLimitPartitionTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Behind_the_private_edge_each_visitor_gets_their_own_email_bucket()
    {
        await SendAsync("172.17.0.1", "203.0.113.7", "/api/auth/password-reset");

        _factory.Counter.Hits.ShouldContain(("account-email", "203.0.113.7"));
    }

    [Fact]
    public async Task A_public_peer_cannot_pick_its_bucket_with_a_forged_forwarded_for()
    {
        await SendAsync("198.51.100.9", "203.0.113.8", "/api/session/login");

        _factory.Counter.Hits.ShouldContain(("login", "198.51.100.9"));
        _factory.Counter.Hits.ShouldNotContain(h => h.Partition == "203.0.113.8");
    }

    private async Task SendAsync(string peer, string forwardedFor, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Add(PeerFilter.Header, peer);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        using var response = await _factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }

    public sealed class RecordingCounter : IRateLimitCounter
    {
        public ConcurrentBag<(string Policy, string Partition)> Hits { get; } = [];

        public Task<RateLimitDecision> HitAsync(RateLimitPolicy policy, string partition, CancellationToken cancellationToken)
        {
            Hits.Add((policy.Name, partition));
            return Task.FromResult(new RateLimitDecision(true, policy.Limit, TimeSpan.Zero));
        }
    }

    // TestServer has no socket; this sets the peer address the edge would connect from, before forwarded headers run.
    private sealed class PeerFilter : IStartupFilter
    {
        public const string Header = "X-Test-Peer";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers[Header].ToString());
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        public RecordingCounter Counter { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Redis", "127.0.0.1:1,connectTimeout=200");
            builder.UseSetting("Gateway:IdentityAddress", "http://127.0.0.1:1");
            builder.UseSetting("Kafka:BootstrapServers", "127.0.0.1:9");
            builder.UseSetting("Kafka:Environment", "test");
            builder.UseSetting("Kafka:ClientId", "gateway-tests");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IStartupFilter, PeerFilter>();
                services.AddSingleton<IRateLimitCounter>(Counter);
                services.AddSingleton<ISessionStore, InMemorySessionStore>();
                services.AddSingleton<IHandoffCodes, InMemoryHandoffCodes>();
                services.AddSingleton<ISessionLimits>(FixedSessionLimits.None);
                services.AddSingleton<IIdentityClient>(new FakeIdentity(TimeProvider.System, "operator-1", "Operator"));
            });
        }
    }
}
