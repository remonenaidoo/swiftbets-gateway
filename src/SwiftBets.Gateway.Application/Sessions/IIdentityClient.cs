namespace SwiftBets.Gateway.Application.Sessions;

public interface IIdentityClient
{
    Task<SessionTokens?> SignInAsync(string username, string password, CancellationToken cancellationToken);

    Task<SessionTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Rotates an app's refresh token and issues a separate sign-in for a browser it opens.</summary>
    Task<SessionHandoff?> HandoffAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Best effort: identity stops honouring the refresh token. A failure leaves only a token no session holds.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}
