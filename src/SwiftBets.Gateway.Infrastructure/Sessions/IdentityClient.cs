using System.Net.Http.Json;
using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.Infrastructure.Sessions;

public sealed class IdentityClient(HttpClient http) : IIdentityClient
{
    public Task<SessionTokens?> SignInAsync(string username, string password, CancellationToken cancellationToken) =>
        PostAsync("auth/token", new { grantType = "password", username, password }, cancellationToken);

    public Task<SessionTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        PostAsync("auth/refresh", new { refreshToken }, cancellationToken);

    public async Task<SessionHandoff?> HandoffAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("auth/handoff", new { refreshToken }, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<SessionHandoff>(cancellationToken) : null;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("auth/revoke", new { refreshToken }, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // The session is already gone at the gateway; an unrevoked refresh token expires on its own.
        }
    }

    private async Task<SessionTokens?> PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(path, body, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<SessionTokens>(cancellationToken) : null;
    }
}
