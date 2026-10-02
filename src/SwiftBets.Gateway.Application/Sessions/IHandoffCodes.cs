namespace SwiftBets.Gateway.Application.Sessions;

/// <summary>Single-use codes that carry an app's sign-in into a browser it opens; taking a code deletes it.</summary>
public interface IHandoffCodes
{
    Task StoreAsync(string codeHash, SessionTokens browser, TimeSpan lifetime);

    Task<SessionTokens?> TakeAsync(string codeHash);
}
