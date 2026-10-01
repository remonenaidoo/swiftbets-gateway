using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SwiftBets.Gateway.Api.Sessions;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.TestDoubles;

namespace SwiftBets.Gateway.Api.Tests;

public sealed class BrowserSessionMiddlewareTests
{
    private readonly InMemorySessionStore _store = new();
    private readonly FakeIdentity _identity = new(TimeProvider.System);

    [Fact]
    public async Task Cookie_mutation_with_the_csrf_header_is_forwarded_as_a_bearer_token_without_spoofed_identity()
    {
        var sessionId = await Sessions().StartAsync(_identity.Issue(), "Firefox");

        var (context, forwarded) = await RunAsync($"{SessionCookies.Session}={sessionId}", csrf: true);

        forwarded.ShouldBeTrue();
        context.Request.Headers.Authorization.ToString().ShouldStartWith("Bearer ");
        context.Request.Headers.Authorization.ToString().ShouldNotContain(sessionId);
        context.Request.Headers.ContainsKey("X-User-Id").ShouldBeFalse();
    }

    [Fact]
    public async Task Cookie_mutation_without_the_csrf_header_is_refused()
    {
        var sessionId = await Sessions().StartAsync(_identity.Issue(), "Firefox");

        var (context, forwarded) = await RunAsync($"{SessionCookies.Session}={sessionId}", csrf: false);

        forwarded.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Cookie_session_is_used_even_when_a_proxy_adds_basic_credentials()
    {
        var sessionId = await Sessions().StartAsync(_identity.Issue(), "Firefox");

        var (context, forwarded) = await RunAsync($"{SessionCookies.Session}={sessionId}", csrf: true, authorization: "Basic c3dpZnRiZXRzOnB3");

        forwarded.ShouldBeTrue();
        context.Request.Headers.Authorization.ToString().ShouldStartWith("Bearer ");
    }

    [Fact]
    public async Task Explicit_bearer_token_is_left_as_sent()
    {
        var (context, _) = await RunAsync($"{SessionCookies.Session}={SessionId()}", csrf: true, authorization: "Bearer native.client.token");

        context.Request.Headers.Authorization.ToString().ShouldBe("Bearer native.client.token");
    }

    [Fact]
    public async Task Revoked_session_is_refused_and_its_cookie_cleared()
    {
        var sessions = Sessions();
        var sessionId = await sessions.StartAsync(_identity.Issue(), "Firefox");
        await sessions.RevokeAllAsync("punter-1");

        var (context, forwarded) = await RunAsync($"{SessionCookies.Session}={sessionId}", csrf: true);

        forwarded.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Response.Headers.SetCookie.ToString().ShouldContain($"{SessionCookies.Session}=;");
    }

    [Fact]
    public async Task Browser_signed_in_with_the_old_token_cookies_is_upgraded_to_an_opaque_session()
    {
        var legacy = _identity.Issue();

        var (context, forwarded) = await RunAsync($"{SessionCookies.LegacyAccess}={legacy.AccessToken}; {SessionCookies.LegacyRefresh}={legacy.RefreshToken}", csrf: true);

        forwarded.ShouldBeTrue();
        _store.Count.ShouldBe(1);
        var cookies = context.Response.Headers.SetCookie.ToString();
        cookies.ShouldContain($"{SessionCookies.Session}=");
        cookies.ShouldContain($"{SessionCookies.LegacyRefresh}=;");
        cookies.ShouldNotContain(legacy.AccessToken);
    }

    private BrowserSessions Sessions() => new(_store, _identity, Options.Create(new BrowserSessionOptions()), TimeProvider.System);

    private static string SessionId() => Gateway.Domain.SessionId.New();

    private async Task<(HttpContext Context, bool Forwarded)> RunAsync(string cookie, bool csrf, string? authorization = null)
    {
        var forwarded = false;
        var middleware = new BrowserSessionMiddleware(_ => { forwarded = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Method = "POST";
        context.Request.Path = "/api/coupons";
        context.Request.Headers.Cookie = cookie;
        context.Request.Headers["X-User-Id"] = "someone-else";
        if (csrf)
        {
            context.Request.Headers[SessionCookies.CsrfHeader] = "1";
        }

        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        await middleware.InvokeAsync(context, Sessions(), _identity, Options.Create(new BrowserSessionOptions()));
        return (context, forwarded);
    }
}
