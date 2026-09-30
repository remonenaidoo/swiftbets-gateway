using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SwiftBets.Gateway.Api.Sessions;

namespace SwiftBets.Gateway.Api.Tests;

public sealed class SessionInfoTests(HostTests.Factory factory) : IClassFixture<HostTests.Factory>
{
    [Fact]
    public async Task Signed_in_browser_learns_its_subject_and_roles()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/session", UriKind.Relative));
        request.Headers.Add("Cookie", $"{SessionCookies.Access}={Token(DateTimeOffset.UtcNow.AddMinutes(5))}");

        using var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("subject").GetString().ShouldBe("operator-1");
        body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["Operator"]);
    }

    [Fact]
    public async Task Browser_without_a_session_is_told_it_is_signed_out()
    {
        using var response = await factory.CreateClient().GetAsync(new Uri("/api/session", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string Token(DateTimeOffset expires)
    {
        static string Part(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Part(new { alg = "RS256" })}.{Part(new { sub = "operator-1", role = "Operator", exp = expires.ToUnixTimeSeconds() })}.signature";
    }
}
