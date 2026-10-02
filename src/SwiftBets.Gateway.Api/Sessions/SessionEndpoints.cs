using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.Api.Sessions;

public static class SessionEndpoints
{
    private const int DeviceMaxLength = 160;

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var session = endpoints.MapGroup("/api/session");

        session.MapPost("/login", async (LoginRequest request, HttpContext context, IIdentityClient identity, BrowserSessions sessions, IOptions<BrowserSessionOptions> options) =>
        {
            if (context.Request.Headers[SessionCookies.CsrfHeader] != "1")
            {
                return Error.Validation("csrf_required", $"Send {SessionCookies.CsrfHeader}: 1.").ToHttpResult(context);
            }

            var tokens = await identity.SignInAsync(request.Username, request.Password, context.RequestAborted);
            return tokens is null
                ? new Error("invalid_credentials", "Username or password is incorrect.", ErrorKind.Unauthorized).ToHttpResult(context)
                : await StartAsync(context, tokens, sessions, options);
        });

        // Demo sign-in for an open preview: signs the visitor in as a demo account with no form. `as=punter` is the
        // betting site's account, anything else the operator's. Off unless the accounts are configured; 404 otherwise.
        session.MapPost("/demo", async (string? @as, HttpContext context, IIdentityClient identity, IConfiguration configuration, BrowserSessions sessions, IOptions<BrowserSessionOptions> options) =>
        {
            var tokens = await DemoSignInAsync(@as, context, identity, configuration);
            return tokens.Error is { } error ? error.ToHttpResult(context) : await StartAsync(context, tokens.Value!, sessions, options);
        });

        // The same for native apps, which hold tokens instead of cookies.
        session.MapPost("/demo/token", async (string? @as, HttpContext context, IIdentityClient identity, IConfiguration configuration) =>
        {
            var tokens = await DemoSignInAsync(@as, context, identity, configuration);
            return tokens.Error is { } error
                ? error.ToHttpResult(context)
                : Results.Ok(new { accessToken = tokens.Value!.AccessToken, refreshToken = tokens.Value.RefreshToken, expiresIn = tokens.Value.ExpiresIn });
        });

        // An app opening a hosted account page in its browser: trades the app's refresh token for its rotated tokens
        // and a one-minute, single-use link that signs that browser in as a separate device.
        session.MapPost("/handoff", async (HandoffRequest request, HttpContext context, IIdentityClient identity, IHandoffCodes codes) =>
        {
            if (context.Request.Headers[SessionCookies.CsrfHeader] != "1")
            {
                return Error.Validation("csrf_required", $"Send {SessionCookies.CsrfHeader}: 1.").ToHttpResult(context);
            }

            if (!HandoffPages.Contains(request.Next) || string.IsNullOrEmpty(request.RefreshToken))
            {
                return Error.Validation("invalid_handoff", "next must be an account page.").ToHttpResult(context);
            }

            if (await identity.HandoffAsync(request.RefreshToken, context.RequestAborted) is not { } handoff)
            {
                return new Error("invalid_refresh_token", "Sign in again.", ErrorKind.Unauthorized).ToHttpResult(context);
            }

            var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            await codes.StoreAsync(HashCode(code), handoff.Browser, TimeSpan.FromMinutes(1));
            return Results.Ok(new
            {
                accessToken = handoff.Device.AccessToken,
                refreshToken = handoff.Device.RefreshToken,
                expiresIn = handoff.Device.ExpiresIn,
                url = $"/api/session/handoff/{code}?next={Uri.EscapeDataString(request.Next!)}",
            });
        });

        session.MapGet("/handoff/{code}", async (string code, string? next, HttpContext context, IHandoffCodes codes, BrowserSessions sessions, IOptions<BrowserSessionOptions> options) =>
        {
            var target = HandoffPages.Contains(next) ? next! : "/account";
            if (await codes.TakeAsync(HashCode(code)) is not { } browser)
            {
                return Results.Redirect("/account/sign-in");
            }

            await StartAsync(context, browser, sessions, options);
            return Results.Redirect(target);
        });

        // Who the browser session belongs to, for the UI to decide what to show. Every service still validates the
        // token it receives; this only describes the session back to its owner.
        session.MapGet("/", (HttpContext context, ISessionLimits limits) =>
            Current(context) is { } current && limits.For(current.Session.UserId) is var l
                ? Results.Ok(new SessionInfo(current.Session.UserId, current.Session.Roles, current.Session.AccessExpiresAt, current.Session.CreatedAt, l.SessionLimitMinutes, l.RealityCheckMinutes))
                : Unauthenticated(context));

        session.MapPost("/logout", async (HttpContext context, BrowserSessions sessions) =>
        {
            if (Current(context) is { } current)
            {
                await sessions.EndAsync(current);
            }

            SessionCookies.Clear(context.Response);
            return Results.NoContent();
        });

        session.MapGet("/devices", async (HttpContext context, BrowserSessions sessions) =>
        {
            if (Current(context) is not { } current)
            {
                return Unauthenticated(context);
            }

            var devices = await sessions.ListAsync(current.Session.UserId);
            return Results.Ok(devices.Select(d => new DeviceInfo(d.Session.PublicId, d.Session.Device, d.Session.CreatedAt, d.Session.LastSeenAt, d.Hash == current.SessionHash)));
        });

        session.MapDelete("/devices/{publicId:guid}", async (Guid publicId, HttpContext context, BrowserSessions sessions) =>
        {
            if (Current(context) is not { } current)
            {
                return Unauthenticated(context);
            }

            if (!await sessions.RevokeAsync(current.Session.UserId, publicId))
            {
                return Error.NotFound("session_not_found", "No such session.").ToHttpResult(context);
            }

            if (publicId == current.Session.PublicId)
            {
                SessionCookies.Clear(context.Response);
            }

            return Results.NoContent();
        });

        session.MapDelete("/devices", async (HttpContext context, BrowserSessions sessions) =>
        {
            if (Current(context) is not { } current)
            {
                return Unauthenticated(context);
            }

            await sessions.RevokeAllAsync(current.Session.UserId);
            SessionCookies.Clear(context.Response);
            return Results.NoContent();
        });

        return endpoints;
    }

    public static string DeviceOf(HttpContext context) =>
        context.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent[..Math.Min(agent.Length, DeviceMaxLength)] : "unknown device";

    private static SessionResolution? Current(HttpContext context) =>
        context.Items.TryGetValue(BrowserSessionMiddleware.SessionItem, out var value) ? value as SessionResolution : null;

    private static IResult Unauthenticated(HttpContext context) =>
        new Error("unauthenticated", "No active session.", ErrorKind.Unauthorized).ToHttpResult(context);

    private static readonly HashSet<string?> HandoffPages = ["/account", "/account/wallet", "/account/safer-gambling"];

    private static string HashCode(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private static async Task<IResult> StartAsync(HttpContext context, SessionTokens tokens, BrowserSessions sessions, IOptions<BrowserSessionOptions> options)
    {
        var sessionId = await sessions.StartAsync(tokens, DeviceOf(context));
        SessionCookies.ClearLegacy(context.Response);
        SessionCookies.Write(context.Response, sessionId, TimeSpan.FromDays(options.Value.LifetimeDays));
        return Results.Ok(new { expiresIn = tokens.ExpiresIn });
    }

    private static async Task<(SessionTokens? Value, Error? Error)> DemoSignInAsync(string? role, HttpContext context, IIdentityClient identity, IConfiguration configuration)
    {
        var punter = string.Equals(role, "punter", StringComparison.OrdinalIgnoreCase);
        var username = configuration[punter ? "Gateway:DemoSignIn:PunterUsername" : "Gateway:DemoSignIn:Username"];
        var password = configuration["Gateway:DemoSignIn:Password"];
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return (null, Error.NotFound("demo_sign_in_disabled", "Demo sign-in is not enabled here."));
        }

        if (context.Request.Headers[SessionCookies.CsrfHeader] != "1")
        {
            return (null, Error.Validation("csrf_required", $"Send {SessionCookies.CsrfHeader}: 1."));
        }

        var tokens = await identity.SignInAsync(username, password, context.RequestAborted);
        return tokens is null
            ? (null, new Error("invalid_credentials", "The demo account could not sign in.", ErrorKind.Unauthorized))
            : (tokens, null);
    }

    public sealed record LoginRequest(string Username, string Password);

    public sealed record HandoffRequest(string? RefreshToken, string? Next);

    /// <summary>StartedAt and the two limits let the site show reality checks and how long is left.</summary>
    public sealed record SessionInfo(string Subject, IReadOnlyList<string> Roles, DateTimeOffset ExpiresAt, DateTimeOffset StartedAt, int? SessionLimitMinutes, int? RealityCheckMinutes);

    public sealed record DeviceInfo(Guid Id, string Device, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, bool Current);
}
