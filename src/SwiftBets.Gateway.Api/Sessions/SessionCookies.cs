namespace SwiftBets.Gateway.Api.Sessions;

/// <summary>
/// The browser holds one opaque, httpOnly, Secure, SameSite=Strict cookie scoped to /api. The older cookies carried the
/// tokens themselves; they are read once to upgrade a signed-in browser and then deleted.
/// </summary>
public static class SessionCookies
{
    public const string Session = "sb_sid";
    public const string LegacyAccess = "sb_session";
    public const string LegacyRefresh = "sb_refresh";
    public const string CsrfHeader = "X-SwiftBets-Csrf";

    public static void Write(HttpResponse response, string sessionId, TimeSpan lifetime) =>
        response.Cookies.Append(Session, sessionId, Options(lifetime));

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(Session, Options(TimeSpan.Zero));
        ClearLegacy(response);
    }

    public static void ClearLegacy(HttpResponse response)
    {
        response.Cookies.Delete(LegacyAccess, Options(TimeSpan.Zero));
        response.Cookies.Delete(LegacyRefresh, Options(TimeSpan.Zero));
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
