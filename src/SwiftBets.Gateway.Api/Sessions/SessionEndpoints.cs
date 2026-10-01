using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Gateway.Application.Sessions;
using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Api.Sessions;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var session = endpoints.MapGroup("/api/session");

        session.MapPost("/login", async (LoginRequest request, HttpContext context, IIdentityClient identity) =>
        {
            if (context.Request.Headers[SessionCookies.CsrfHeader] != "1")
            {
                return Error.Validation("csrf_required", $"Send {SessionCookies.CsrfHeader}: 1.").ToHttpResult(context);
            }

            var tokens = await identity.SignInAsync(request.Username, request.Password, context.RequestAborted);
            if (tokens is null)
            {
                return new Error("invalid_credentials", "Username or password is incorrect.", ErrorKind.Unauthorized).ToHttpResult(context);
            }

            SessionCookies.Write(context.Response, tokens);
            return Results.Ok(new { expiresIn = tokens.ExpiresIn });
        });

        // Demo sign-in for an open preview: signs the visitor in as a demo account with no form. `as=punter` is the
        // betting site's account, anything else the operator's. Off unless the accounts are configured; 404 otherwise.
        session.MapPost("/demo", async (string? @as, HttpContext context, IIdentityClient identity, IConfiguration configuration) =>
        {
            var tokens = await DemoSignInAsync(@as, context, identity, configuration);
            if (tokens.Error is { } error)
            {
                return error.ToHttpResult(context);
            }

            SessionCookies.Write(context.Response, tokens.Value!);
            return Results.Ok(new { expiresIn = tokens.Value!.ExpiresIn });
        });

        // The same for native apps, which hold tokens instead of cookies.
        session.MapPost("/demo/token", async (string? @as, HttpContext context, IIdentityClient identity, IConfiguration configuration) =>
        {
            var tokens = await DemoSignInAsync(@as, context, identity, configuration);
            return tokens.Error is { } error
                ? error.ToHttpResult(context)
                : Results.Ok(new { accessToken = tokens.Value!.AccessToken, refreshToken = tokens.Value.RefreshToken, expiresIn = tokens.Value.ExpiresIn });
        });

        // Who the browser session belongs to, for the UI to decide what to show. The token is not verified here:
        // this only describes the cookie back to its owner, and every service still validates it on each call.
        session.MapGet("/", (HttpContext context, TimeProvider time) =>
            context.Request.Cookies[SessionCookies.Access] is { } access && JwtClaims.ReadExpiry(access) is { } expiry && expiry > time.GetUtcNow()
                ? Results.Ok(new SessionInfo(JwtClaims.ReadString(access, "sub") ?? string.Empty, JwtClaims.ReadStrings(access, "role"), expiry))
                : new Error("unauthenticated", "No active session.", ErrorKind.Unauthorized).ToHttpResult(context));

        session.MapPost("/logout", (HttpContext context) =>
        {
            SessionCookies.Clear(context.Response);
            return Results.NoContent();
        });

        return endpoints;
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

    public sealed record SessionInfo(string Subject, IReadOnlyList<string> Roles, DateTimeOffset ExpiresAt);
}
