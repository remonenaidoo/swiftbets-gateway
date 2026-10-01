using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.Domain;
using SwiftBets.Gateway.TestDoubles;

namespace SwiftBets.Gateway.Application.Tests;

public sealed class BrowserSessionsTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemorySessionStore _store = new();

    [Fact]
    public async Task Signed_in_cookie_resolves_to_its_session_without_refreshing()
    {
        var (sessions, identity) = Create(_time);
        var sessionId = await sessions.StartAsync(identity.Issue(), "Firefox");

        var resolution = (await sessions.ResolveAsync(sessionId, CancellationToken.None))!;

        (resolution.Session.UserId, resolution.Session.Device, resolution.NewSessionId).ShouldBe(("punter-1", "Firefox", (string?)null));
        resolution.Session.Roles.ShouldBe(["Punter"]);
        identity.RefreshCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Refresh_near_expiry_rotates_the_cookie_and_the_old_cookie_follows_it()
    {
        var (sessions, identity) = Create(_time);
        var sessionId = await sessions.StartAsync(identity.Issue(), "Firefox");
        var first = (await sessions.ResolveAsync(sessionId, CancellationToken.None))!;
        _time.Advance(TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(45));

        var refreshed = (await sessions.ResolveAsync(sessionId, CancellationToken.None))!;
        var followed = (await sessions.ResolveAsync(sessionId, CancellationToken.None))!;

        refreshed.NewSessionId.ShouldNotBeNull();
        refreshed.NewSessionId.ShouldNotBe(sessionId);
        refreshed.Session.AccessToken.ShouldNotBe(first.Session.AccessToken);
        refreshed.Session.PublicId.ShouldBe(first.Session.PublicId);
        (followed.NewSessionId, followed.Session.AccessToken).ShouldBe((refreshed.NewSessionId, refreshed.Session.AccessToken));
        identity.RefreshCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Concurrent_requests_near_expiry_use_the_single_use_refresh_token_once()
    {
        var (sessions, identity) = Create(TimeProvider.System);
        identity.AccessLifetime = TimeSpan.FromSeconds(10);
        var sessionId = await sessions.StartAsync(identity.Issue(), "Firefox");
        identity.AccessLifetime = TimeSpan.FromMinutes(10);
        identity.RefreshDelay = TimeSpan.FromMilliseconds(300);

        var resolutions = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => sessions.ResolveAsync(sessionId, CancellationToken.None)));

        identity.RefreshCalls.ShouldBe(1);
        resolutions.ShouldAllBe(r => r != null && r.NewSessionId != null);
        resolutions.Select(r => r!.Session.AccessToken).Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_refused_by_identity_signs_the_session_out()
    {
        var (sessions, identity) = Create(_time);
        var sessionId = await sessions.StartAsync(identity.Issue(), "Firefox");
        identity.RefuseRefresh = true;
        _time.Advance(TimeSpan.FromMinutes(10));

        (await sessions.ResolveAsync(sessionId, CancellationToken.None)).ShouldBeNull();
        _store.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Revoked_device_no_longer_resolves_while_the_others_do()
    {
        var (sessions, identity) = Create(_time);
        var phone = await sessions.StartAsync(identity.Issue(), "Phone");
        var laptop = await sessions.StartAsync(identity.Issue(), "Laptop");
        var phoneSession = (await sessions.ResolveAsync(phone, CancellationToken.None))!.Session;

        (await sessions.RevokeAsync("punter-1", phoneSession.PublicId)).ShouldBeTrue();

        (await sessions.ResolveAsync(phone, CancellationToken.None)).ShouldBeNull();
        (await sessions.ResolveAsync(laptop, CancellationToken.None)).ShouldNotBeNull();
        (await sessions.ListAsync("punter-1")).Select(s => s.Session.Device).ShouldBe(["Laptop"]);
    }

    [Fact]
    public async Task Revoking_every_session_signs_out_every_device()
    {
        var (sessions, identity) = Create(_time);
        var phone = await sessions.StartAsync(identity.Issue(), "Phone");
        var laptop = await sessions.StartAsync(identity.Issue(), "Laptop");

        await sessions.RevokeAllAsync("punter-1");

        (await sessions.ResolveAsync(phone, CancellationToken.None)).ShouldBeNull();
        (await sessions.ResolveAsync(laptop, CancellationToken.None)).ShouldBeNull();
        identity.Revoked.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Signing_out_also_stops_identity_honouring_the_refresh_token()
    {
        var (sessions, identity) = Create(_time);
        var tokens = identity.Issue();
        var sessionId = await sessions.StartAsync(tokens, "Firefox");

        await sessions.EndAsync((await sessions.ResolveAsync(sessionId, CancellationToken.None))!);

        identity.Revoked.ShouldBe([tokens.RefreshToken]);
        (await identity.RefreshAsync(tokens.RefreshToken, CancellationToken.None)).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("header.payload.signature")]
    [InlineData("not a session id at all, but long enough!!")]
    public async Task Malformed_cookie_resolves_to_no_session(string? sessionId)
    {
        var (sessions, _) = Create(_time);

        (await sessions.ResolveAsync(sessionId, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public void Session_ids_are_unguessable_well_formed_and_stored_only_as_hashes()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => SessionId.New()).ToList();

        ids.Distinct().Count().ShouldBe(100);
        ids.ShouldAllBe(id => SessionId.IsWellFormed(id));
        SessionId.Hash(ids[0]).ShouldBe(SessionId.Hash(ids[0]));
        SessionId.Hash(ids[0]).ShouldNotContain(ids[0]);
    }

    private (BrowserSessions Sessions, FakeIdentity Identity) Create(TimeProvider time)
    {
        var identity = new FakeIdentity(time);
        return (new BrowserSessions(_store, identity, Options.Create(new BrowserSessionOptions()), time), identity);
    }
}
