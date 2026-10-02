namespace SwiftBets.Gateway.Application.Sessions;

public sealed record SessionTokens(string AccessToken, string RefreshToken, int ExpiresIn);

/// <summary>The app's rotated tokens and a separate sign-in for the browser it opens.</summary>
public sealed record SessionHandoff(SessionTokens Device, SessionTokens Browser);
