using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Application.Sessions;

/// <summary>Either a live session, or a pointer to the cookie value that replaced it during the rotation grace period.</summary>
public sealed record SessionEntry(BrowserSession? Session, string? RotatedTo);
