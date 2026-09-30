using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Gateway.Application.Sessions;

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

        session.MapPost("/logout", (HttpContext context) =>
        {
            SessionCookies.Clear(context.Response);
            return Results.NoContent();
        });

        return endpoints;
    }

    public sealed record LoginRequest(string Username, string Password);
}
