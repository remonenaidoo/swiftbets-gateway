using System.Text;
using System.Text.Json;

namespace SwiftBets.Gateway.Domain;

/// <summary>
/// Reads a token's exp claim without validating it. The gateway only uses this to decide when to refresh a browser
/// session; every downstream service still validates the signature, issuer, audience and lifetime itself.
/// </summary>
public static class JwtExpiry
{
    public static DateTimeOffset? Read(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return document.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
