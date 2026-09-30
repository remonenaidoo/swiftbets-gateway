using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.Api.Sessions;

/// <summary>The browser never sees a token: both live in httpOnly, Secure, SameSite=Strict cookies scoped to /api.</summary>
public static class SessionCookies
{
    public const string Access = "sb_session";
    public const string Refresh = "sb_refresh";
    public const string CsrfHeader = "X-SwiftBets-Csrf";

    public static void Write(HttpResponse response, SessionTokens tokens)
    {
        response.Cookies.Append(Access, tokens.AccessToken, Options(TimeSpan.FromSeconds(tokens.ExpiresIn + 60)));
        response.Cookies.Append(Refresh, tokens.RefreshToken, Options(TimeSpan.FromDays(7)));
    }

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(Access, Options(TimeSpan.Zero));
        response.Cookies.Delete(Refresh, Options(TimeSpan.Zero));
    }

    private static CookieOptions Options(TimeSpan maxAge) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api",
        MaxAge = maxAge,
        IsEssential = true,
    };
}
