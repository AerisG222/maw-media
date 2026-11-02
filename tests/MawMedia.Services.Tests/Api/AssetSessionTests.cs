using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models;

namespace MawMedia.Services.Tests.Api;

// the cookie a browser loads media with.  the tests that matter most are the ones
// about what it can not do, because it is sent automatically by the browser.
public class AssetSessionTests
    : ApiTestBase
{
    const string ROUTE = "/api/v1/auth/asset-session";

    static readonly Uri Asset = new(Constants.FILE_NATURE_1.Path, UriKind.Relative);

    public AssetSessionTests(TestFixture fixture)
        : base(fixture)
    {

    }

    [Fact]
    public async Task CreatingRequiresABearerToken()
    {
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.PostAsync(ROUTE, null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CookieIsLockedDownAndBounded()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.PostAsync(ROUTE, null, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.StartsWith("__Host-", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/;", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);

        var session = await response.Content.ReadFromJsonAsync<AssetSession>(JsonOptions, token);

        Assert.NotNull(session);
        Assert.InRange(session.ExpiresInSeconds, 1, 3600);
    }

    [Fact]
    public async Task CookieNeverOutlivesTheTokenThatVouchedForIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        client.DefaultRequestHeaders.Add(
            TestAuthHandler.HEADER_EXP,
            DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

        var response = await client.PostAsync(ROUTE, null, token);
        var session = await response.Content.ReadFromJsonAsync<AssetSession>(JsonOptions, token);

        Assert.NotNull(session);
        Assert.InRange(session.ExpiresInSeconds, 1, 300);
    }

    [Fact]
    public async Task LongLivedTokenIsStillCapped()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        client.DefaultRequestHeaders.Add(
            TestAuthHandler.HEADER_EXP,
            DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

        var response = await client.PostAsync(ROUTE, null, token);
        var session = await response.Content.ReadFromJsonAsync<AssetSession>(JsonOptions, token);

        Assert.NotNull(session);
        Assert.InRange(session.ExpiresInSeconds, 3590, 3600);
    }

    [Fact]
    public async Task CookieLoadsAnAssetTheCallerMaySee()
    {
        var cookie = await AssetCookie(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await SendWithCookie(HttpMethod.Get, Asset, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CookieDoesNotGetPastTheAssetRules()
    {
        // the token had no media:read, so neither does the cookie
        var cookie = await AssetCookie(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.StatsRead);

        var response = await SendWithCookie(HttpMethod.Get, Asset, cookie);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CookieDoesNotHelpAUserWhoIsNotActivated()
    {
        var cookie = await AssetCookie(Constants.EXTERNAL_ID_NOUSER, ApiScopes.MediaRead);

        var response = await SendWithCookie(HttpMethod.Get, Asset, cookie);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/account-status")]
    [InlineData("/api/v1/config/scales")]
    public async Task CookieIsNotAcceptedByTheApi(string route)
    {
        var cookie = await AssetCookie(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await SendWithCookie(HttpMethod.Get, new Uri(route, UriKind.Relative), cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CookieCanNotRenewItself()
    {
        var cookie = await AssetCookie(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await SendWithCookie(HttpMethod.Post, new Uri(ROUTE, UriKind.Relative), cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedCookieIsRefused()
    {
        var cookie = await AssetCookie(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await SendWithCookie(HttpMethod.Get, Asset, cookie + "x");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BadAuthorizationHeaderIsNotRescuedByACookie()
    {
        var token = TestContext.Current.CancellationToken;
        var cookie = await AssetCookie(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        using var anonymous = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Asset);

        request.Headers.Add("Cookie", cookie);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer not-a-token");

        var response = await anonymous.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteExpiresTheCookieWithoutAToken()
    {
        var token = TestContext.Current.CancellationToken;
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.DeleteAsync(ROUTE, token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.StartsWith("__Host-", cookie, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ApiFactory.ORIGIN)]
    public async Task AssetResponseVariesByOrigin(string? origin)
    {
        var token = TestContext.Current.CancellationToken;
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var request = new HttpRequestMessage(HttpMethod.Get, Asset);

        if (origin != null)
        {
            request.Headers.Add("Origin", origin);
        }

        var response = await client.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.Vary, v => string.Equals(v, "Origin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CredentialedCorsLoadIsAllowed()
    {
        var token = TestContext.Current.CancellationToken;
        var cookie = await AssetCookie(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        using var anonymous = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Asset);

        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("Origin", ApiFactory.ORIGIN);

        var response = await anonymous.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ApiFactory.ORIGIN, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }
}
