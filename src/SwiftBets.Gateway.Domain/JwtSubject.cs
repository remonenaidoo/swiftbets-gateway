namespace SwiftBets.Gateway.Domain;

/// <summary>Reads the unverified sub claim purely to partition rate limits; a forged token only buys itself a 401 downstream.</summary>
public static class JwtSubject
{
    public static string? Read(string? authorization) =>
        authorization is { Length: > 7 } header && header.StartsWith("Bearer ", StringComparison.Ordinal)
            ? JwtClaims.ReadString(header[7..], "sub")
            : null;
}
