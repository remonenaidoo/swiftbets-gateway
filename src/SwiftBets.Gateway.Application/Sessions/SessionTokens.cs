namespace SwiftBets.Gateway.Application.Sessions;

public sealed record SessionTokens(string AccessToken, string RefreshToken, int ExpiresIn);
