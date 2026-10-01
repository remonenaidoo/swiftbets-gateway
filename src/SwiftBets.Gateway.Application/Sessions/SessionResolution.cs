using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Application.Sessions;

/// <summary>The session for a cookie, and the new cookie value when the session was rotated on this request.</summary>
public sealed record SessionResolution(BrowserSession Session, string SessionHash, string? NewSessionId);
