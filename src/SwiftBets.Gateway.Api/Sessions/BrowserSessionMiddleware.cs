using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Api.Sessions;

/// <summary>
/// Turns a browser session cookie into a bearer token for the proxied request, refreshing it shortly before expiry.
/// A cookie-authenticated mutation must also carry the CSRF header, which a cross-site form or script cannot add.
/// Identity headers from the client are always stripped so nothing downstream can be spoofed.
/// </summary>
public sealed class BrowserSessionMiddleware(RequestDelegate next, TimeProvider time)
{
    private static readonly string[] SpoofableHeaders = ["X-User-Id", "X-User-Roles", "X-Forwarded-User", "X-Authenticated-User"];

    public async Task InvokeAsync(HttpContext context, IIdentityClient identity)
    {
        foreach (var header in SpoofableHeaders)
        {
            context.Request.Headers.Remove(header);
        }

        var access = context.Request.Cookies[SessionCookies.Access];
        if (access is null || context.Request.Headers.Authorization.Count > 0)
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

        if (JwtExpiry.Read(access) is { } expiry && expiry - time.GetUtcNow() < TimeSpan.FromSeconds(30)
            && context.Request.Cookies[SessionCookies.Refresh] is { } refresh)
        {
            var renewed = await identity.RefreshAsync(refresh, context.RequestAborted);
            if (renewed is null)
            {
                SessionCookies.Clear(context.Response);
                await ErrorEnvelopes.WriteAsync(context, ErrorEnvelopes.Create(context, StatusCodes.Status401Unauthorized, "session_expired", "Sign in again."));
                return;
            }

            SessionCookies.Write(context.Response, renewed);
            access = renewed.AccessToken;
        }

        context.Request.Headers.Authorization = $"Bearer {access}";
        await next(context);
    }
}
