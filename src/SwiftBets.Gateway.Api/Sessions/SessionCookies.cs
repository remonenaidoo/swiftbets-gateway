namespace SwiftBets.Gateway.Api.Sessions;

/// <summary>
/// The browser holds one opaque, httpOnly, Secure, SameSite=Strict cookie for the whole site, so server-rendered pages see
/// the session too (D113). The older cookies carried the tokens themselves, or were scoped to /api; they are deleted.
/// </summary>
public static class SessionCookies
{
    public const string Session = "sb_sid";
    public const string LegacyAccess = "sb_session";
    public const string LegacyRefresh = "sb_refresh";
    public const string CsrfHeader = "X-SwiftBets-Csrf";

    private const string LegacyPath = "/api";

    public static void Write(HttpResponse response, string sessionId, TimeSpan lifetime)
    {
        response.Cookies.Delete(Session, Options(TimeSpan.Zero, LegacyPath));
        response.Cookies.Append(Session, sessionId, Options(lifetime));
    }

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(Session, Options(TimeSpan.Zero, LegacyPath));
        response.Cookies.Delete(Session, Options(TimeSpan.Zero));
        ClearLegacy(response);
    }

    public static void ClearLegacy(HttpResponse response)
    {
        response.Cookies.Delete(LegacyAccess, Options(TimeSpan.Zero, LegacyPath));
        response.Cookies.Delete(LegacyRefresh, Options(TimeSpan.Zero, LegacyPath));
    }

    private static CookieOptions Options(TimeSpan maxAge, string path = "/") => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = path,
        MaxAge = maxAge,
        IsEssential = true,
    };
}
