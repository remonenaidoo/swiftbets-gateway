namespace SwiftBets.Gateway.Domain;

/// <summary>
/// One signed-in browser. <see cref="PublicId"/> names the session in device lists; it survives rotation of the cookie
/// value and is useless for authenticating.
/// </summary>
public sealed record BrowserSession(
    Guid PublicId,
    string UserId,
    IReadOnlyList<string> Roles,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    string Device)
{
    public bool NeedsRefresh(DateTimeOffset now, TimeSpan ahead) => AccessExpiresAt - now < ahead;
}
