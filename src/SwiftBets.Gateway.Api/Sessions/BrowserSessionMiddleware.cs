using Microsoft.Extensions.Options;
using StackExchange.Redis;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.Api.Sessions;

/// <summary>
/// Turns the opaque session cookie into a bearer token for the proxied request; the token itself never reaches the
/// browser. A cookie-authenticated mutation must also carry the CSRF header, which a cross-site form or script cannot
/// add. Identity headers from the client are always stripped so nothing downstream can be spoofed.
/// </summary>
public sealed class BrowserSessionMiddleware(RequestDelegate next)
{
    public const string SessionItem = "swiftbets.session";

    private static readonly string[] SpoofableHeaders = ["X-User-Id", "X-User-Roles", "X-Forwarded-User", "X-Authenticated-User"];

    public async Task InvokeAsync(HttpContext context, BrowserSessions sessions, IIdentityClient identity, IOptions<BrowserSessionOptions> options)
    {
        foreach (var header in SpoofableHeaders)
        {
            context.Request.Headers.Remove(header);
        }

        // An explicit bearer token (a native client) wins. Any other Authorization header, such as Basic credentials
        // from a password-gated preview proxy in front of the gateway, is not ours and must not hide the session.
        var hasBearer = context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        var sessionId = context.Request.Cookies[SessionCookies.Session];
        var legacyRefresh = context.Request.Cookies[SessionCookies.LegacyRefresh];
        if (hasBearer || (sessionId is null && legacyRefresh is null))
        {
            await next(context);
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && context.Request.Headers[SessionCookies.CsrfHeader] != "1")
        {
            await ErrorEnvelopes.WriteAsync(context, ErrorEnvelopes.Create(context, StatusCodes.Status403Forbidden, "csrf_required", $"Cookie-authenticated requests must send {SessionCookies.CsrfHeader}: 1."));
            return;
        }

        SessionResolution? resolution;
        try
        {
            resolution = sessionId is not null
                ? await sessions.ResolveAsync(sessionId, context.RequestAborted)
                : await UpgradeAsync(context, sessions, identity, legacyRefresh!);
        }
        catch (RedisException)
        {
            await ErrorEnvelopes.WriteAsync(context, ErrorEnvelopes.Create(context, StatusCodes.Status503ServiceUnavailable, "session_store_unavailable", "Sessions are briefly unavailable; retry shortly."));
            return;
        }

        if (resolution is null)
        {
            SessionCookies.Clear(context.Response);
            await ErrorEnvelopes.WriteAsync(context, ErrorEnvelopes.Create(context, StatusCodes.Status401Unauthorized, "session_expired", "Sign in again."));
            return;
        }

        if (resolution.NewSessionId is { } renewed)
        {
            SessionCookies.Write(context.Response, renewed, TimeSpan.FromDays(options.Value.LifetimeDays));
        }

        context.Items[SessionItem] = resolution;
        context.Request.Headers.Authorization = $"Bearer {resolution.Session.AccessToken}";
        await next(context);
    }

    /// <summary>A browser signed in before opaque sessions: trade its refresh cookie for a session, once.</summary>
    private static async Task<SessionResolution?> UpgradeAsync(HttpContext context, BrowserSessions sessions, IIdentityClient identity, string legacyRefresh)
    {
        SessionCookies.ClearLegacy(context.Response);
        var tokens = await identity.RefreshAsync(legacyRefresh, context.RequestAborted);
        if (tokens is null)
        {
            return null;
        }

        var sessionId = await sessions.StartAsync(tokens, SessionEndpoints.DeviceOf(context));
        var resolution = await sessions.ResolveAsync(sessionId, context.RequestAborted);
        return resolution is null ? null : resolution with { NewSessionId = sessionId };
    }
}
