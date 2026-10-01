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

        // Demo sign-in for an open preview: signs the browser in as the configured demo account with no form.
        // Off unless Gateway:DemoSignIn:Username and Password are configured; 404 otherwise.
        session.MapPost("/demo", async (HttpContext context, IIdentityClient identity, IConfiguration configuration) =>
        {
            var username = configuration["Gateway:DemoSignIn:Username"];
            var password = configuration["Gateway:DemoSignIn:Password"];
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return Error.NotFound("demo_sign_in_disabled", "Demo sign-in is not enabled here.").ToHttpResult(context);
            }

            if (context.Request.Headers[SessionCookies.CsrfHeader] != "1")
            {
                return Error.Validation("csrf_required", $"Send {SessionCookies.CsrfHeader}: 1.").ToHttpResult(context);
            }

            var tokens = await identity.SignInAsync(username, password, context.RequestAborted);
            if (tokens is null)
            {
                return new Error("invalid_credentials", "The demo account could not sign in.", ErrorKind.Unauthorized).ToHttpResult(context);
            }

            SessionCookies.Write(context.Response, tokens);
            return Results.Ok(new { expiresIn = tokens.ExpiresIn });
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

    public sealed record LoginRequest(string Username, string Password);

    public sealed record SessionInfo(string Subject, IReadOnlyList<string> Roles, DateTimeOffset ExpiresAt);
}
