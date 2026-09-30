namespace SwiftBets.Gateway.Application.Sessions;

public interface IIdentityClient
{
    Task<SessionTokens?> SignInAsync(string username, string password, CancellationToken cancellationToken);

    Task<SessionTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
