using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Contracts.Identity;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.Infrastructure.Messaging;
using SwiftBets.Gateway.Infrastructure.Sessions;
using SwiftBets.Gateway.TestDoubles;

namespace SwiftBets.Gateway.Infrastructure.Tests;

public sealed class SessionRevokedConsumerTests(RedisFixture redis)
{
    [Fact]
    public async Task Revoking_every_session_signs_the_customer_out_of_every_browser()
    {
        var (sessions, consumer, identity, userId) = await CreateAsync();
        var phone = await sessions.StartAsync(identity.Issue(), "Phone");
        var laptop = await sessions.StartAsync(identity.Issue(), "Laptop");

        await consumer.HandleAsync(Event(new SessionRevokedV1(userId, null, "account Suspended", DateTimeOffset.UtcNow)), CancellationToken.None);

        (await sessions.ResolveAsync(phone, CancellationToken.None)).ShouldBeNull();
        (await sessions.ResolveAsync(laptop, CancellationToken.None)).ShouldBeNull();
        identity.Revoked.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_single_device_revocation_leaves_the_other_sessions_alone()
    {
        var (sessions, consumer, identity, userId) = await CreateAsync();
        var phone = await sessions.StartAsync(identity.Issue(), "Phone");

        await consumer.HandleAsync(Event(new SessionRevokedV1(userId, Guid.NewGuid(), "signed out", DateTimeOffset.UtcNow)), CancellationToken.None);

        (await sessions.ResolveAsync(phone, CancellationToken.None)).ShouldNotBeNull();
    }

    private async Task<(BrowserSessions Sessions, SessionRevokedConsumer Consumer, FakeIdentity Identity, Guid UserId)> CreateAsync()
    {
        var userId = Guid.NewGuid();
        var identity = new FakeIdentity(TimeProvider.System, userId.ToString());
        var store = new RedisSessionStore(await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString));
        var sessions = new BrowserSessions(store, identity, Options.Create(new BrowserSessionOptions()), TimeProvider.System);
        return (sessions, new SessionRevokedConsumer(sessions, NullLogger<SessionRevokedConsumer>.Instance), identity, userId);
    }

    private static ConsumedEvent<SessionRevokedV1> Event(SessionRevokedV1 payload) =>
        new(EventEnvelope<SessionRevokedV1>.Create(payload, DateTimeOffset.UtcNow, "corr-revoke"), "swiftbets.identity.session-revoked.v1.test", 0, 0, new Dictionary<string, string>());
}
