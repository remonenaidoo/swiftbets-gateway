using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SwiftBets.Gateway.Api.Sessions;

namespace SwiftBets.Gateway.Api.Tests;

public sealed class SessionInfoTests(HostTests.Factory factory) : IClassFixture<HostTests.Factory>
{
    [Fact]
    public async Task Signed_in_browser_holds_only_an_opaque_cookie_and_learns_its_subject_and_roles()
    {
        var client = factory.CreateClient();
        var cookie = await SignInAsync(client);

        using var response = await SendAsync(client, HttpMethod.Get, "/api/session", cookie);

        cookie.ShouldNotContain(".");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("subject").GetString().ShouldBe("operator-1");
        body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["Operator"]);
        body.GetProperty("permissions").ValueKind.ShouldBe(JsonValueKind.Array);
        body.GetProperty("startedAt").GetDateTimeOffset().ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
        body.GetProperty("sessionLimitMinutes").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Browser_lists_its_devices_and_signing_out_everywhere_ends_them()
    {
        var client = factory.CreateClient();
        var laptop = await SignInAsync(client, "Laptop");
        var phone = await SignInAsync(client, "Phone");

        using var devices = await SendAsync(client, HttpMethod.Get, "/api/session/devices", laptop);
        var listed = await devices.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        listed.EnumerateArray().Select(d => d.GetProperty("device").GetString()).ShouldContain("Phone");
        listed.EnumerateArray().Single(d => d.GetProperty("current").GetBoolean()).GetProperty("device").GetString().ShouldBe("Laptop");

        using var everywhere = await SendAsync(client, HttpMethod.Delete, "/api/session/devices", laptop);
        everywhere.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var afterwards = await SendAsync(client, HttpMethod.Get, "/api/session", phone);
        afterwards.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_password_creates_no_session()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/session/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new { username = "operator1", password = "wrong" }),
        };
        request.Headers.Add(SessionCookies.CsrfHeader, "1");

        using var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Browser_without_a_session_is_told_it_is_signed_out()
    {
        using var response = await factory.CreateClient().GetAsync(new Uri("/api/session", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Demo_sign_in_is_off_unless_configured()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/session/demo", UriKind.Relative));
        request.Headers.Add("X-SwiftBets-Csrf", "1");

        using var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Native_demo_sign_in_is_off_unless_configured()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/session/demo/token?as=punter", UriKind.Relative));
        request.Headers.Add("X-SwiftBets-Csrf", "1");

        using var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<string> SignInAsync(HttpClient client, string device = "Firefox")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/session/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new { username = "operator1", password = "right" }),
        };
        request.Headers.Add(SessionCookies.CsrfHeader, "1");
        request.Headers.UserAgent.ParseAdd(device);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookies = response.Headers.GetValues("Set-Cookie").Where(c => c.StartsWith($"{SessionCookies.Session}=", StringComparison.Ordinal)).ToList();
        var setCookie = cookies.Single(c => !c.StartsWith($"{SessionCookies.Session}=;", StringComparison.Ordinal));
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
        setCookie.ShouldContain("path=/;", Case.Insensitive);
        cookies.ShouldContain(c => c.StartsWith($"{SessionCookies.Session}=;", StringComparison.Ordinal) && c.Contains("path=/api", StringComparison.OrdinalIgnoreCase));
        return setCookie.Split(';')[0][(SessionCookies.Session.Length + 1)..];
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string sessionId)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("Cookie", $"{SessionCookies.Session}={sessionId}");
        request.Headers.Add(SessionCookies.CsrfHeader, "1");
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
