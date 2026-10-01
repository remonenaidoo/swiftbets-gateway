using System.Security.Cryptography;
using System.Text;

namespace SwiftBets.Gateway.Domain;

/// <summary>
/// The browser's session cookie value: 256 random bits, meaningless on its own. The store keys sessions by its SHA-256
/// hash, so a copy of the store does not hold usable cookies.
/// </summary>
public static class SessionId
{
    private const int Length = 43;

    public static string New() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string sessionId) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(sessionId)));

    public static bool IsWellFormed(string? sessionId) =>
        sessionId is { Length: Length } && sessionId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
