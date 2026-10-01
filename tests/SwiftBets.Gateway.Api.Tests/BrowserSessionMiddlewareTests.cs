using Microsoft.AspNetCore.Http;
using SwiftBets.Gateway.Api.Sessions;
using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.Api.Tests;

public sealed class BrowserSessionMiddlewareTests
{
    [Fact]
    public async Task Cookie_mutation_with_the_csrf_header_is_forwarded_as_a_bearer_token_without_spoofed_identity()
    {
        var (context, forwarded) = await RunAsync(csrf: true);

        forwarded.ShouldBeTrue();
        context.Request.Headers.Authorization.ToString().ShouldBe("Bearer header.payload.signature");
        context.Request.Headers.ContainsKey("X-User-Id").ShouldBeFalse();
    }

    [Fact]
    public async Task Cookie_mutation_without_the_csrf_header_is_refused()
    {
        var (context, forwarded) = await RunAsync(csrf: false);

        forwarded.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Cookie_session_is_used_even_when_a_proxy_adds_basic_credentials()
    {
        var (context, forwarded) = await RunAsync(csrf: true, authorization: "Basic c3dpZnRiZXRzOnB3");

        forwarded.ShouldBeTrue();
        context.Request.Headers.Authorization.ToString().ShouldBe("Bearer header.payload.signature");
    }

    [Fact]
    public async Task Explicit_bearer_token_is_left_as_sent()
    {
        var (context, _) = await RunAsync(csrf: true, authorization: "Bearer native.client.token");

        context.Request.Headers.Authorization.ToString().ShouldBe("Bearer native.client.token");
    }

    private static async Task<(HttpContext Context, bool Forwarded)> RunAsync(bool csrf, string? authorization = null)
    {
        var forwarded = false;
        var middleware = new BrowserSessionMiddleware(_ => { forwarded = true; return Task.CompletedTask; }, TimeProvider.System);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Method = "POST";
        context.Request.Path = "/api/coupons";
        context.Request.Headers.Cookie = $"{SessionCookies.Access}=header.payload.signature";
        context.Request.Headers["X-User-Id"] = "someone-else";
        if (csrf)
        {
            context.Request.Headers[SessionCookies.CsrfHeader] = "1";
        }

        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        await middleware.InvokeAsync(context, new NoIdentity());
        return (context, forwarded);
    }

    private sealed class NoIdentity : IIdentityClient
    {
        public Task<SessionTokens?> SignInAsync(string username, string password, CancellationToken cancellationToken) => Task.FromResult<SessionTokens?>(null);

        public Task<SessionTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken) => Task.FromResult<SessionTokens?>(null);
    }
}
