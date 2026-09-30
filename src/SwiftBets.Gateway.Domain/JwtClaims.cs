using System.Text;
using System.Text.Json;

namespace SwiftBets.Gateway.Domain;

/// <summary>
/// Reads claims from a token without validating it. The gateway only uses this to decide when to refresh a browser
/// session; every downstream service still validates the signature, issuer, audience and lifetime itself.
/// </summary>
public static class JwtClaims
{
    public static DateTimeOffset? ReadExpiry(string token) =>
        Payload(token) is { } payload && payload.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    public static string? ReadString(string token, string claim) =>
        Payload(token) is { } payload && payload.TryGetProperty(claim, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>A claim that may be a single string or an array of strings (roles are either, depending on count).</summary>
    public static IReadOnlyList<string> ReadStrings(string token, string claim) =>
        Payload(token) is { } payload && payload.TryGetProperty(claim, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => [value.GetString()!],
                JsonValueKind.Array => [.. value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!)],
                _ => [],
            }
            : [];

    private static JsonElement? Payload(string token)
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
            return document.RootElement.Clone();
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
