using System.Net;
using MawMedia;

namespace MawMedia.Services.Tests.Api;

// the browser half of the api contract.  a rejected preflight fails before the
// request reaches an endpoint, so every other test in this project passes while
// the app in a browser cannot make the call at all - which is exactly how DELETE
// shipped unusable once clans introduced the api's first one.
public class CorsTests
    : ApiTestBase
{
    public CorsTests(TestFixture fixture)
        : base(fixture)
    {

    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task PreflightAllowsEveryVerbTheRoutesExpose(string method)
    {
        var response = await Preflight(ApiFactory.ORIGIN, method);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // the allow headers come back only when the policy actually matched.  a
        // rejected preflight is a bare 204 carrying none of them, which is why
        // the status alone proves nothing here.
        Assert.Equal(
            ApiFactory.ORIGIN,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));

        Assert.Contains(
            method,
            response.Headers
                .GetValues("Access-Control-Allow-Methods")
                .SelectMany(v => v.Split(','))
                .Select(v => v.Trim()));
    }

    [Fact]
    public async Task PreflightRejectsAnUnknownOrigin()
    {
        var response = await Preflight("https://not-ours.example.com", "DELETE");

        // no allow-origin header means the browser blocks it, which is the whole
        // point of naming the origins rather than allowing any
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    async Task<HttpResponseMessage> Preflight(string origin, string method)
    {
        using var client = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/clans");

        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
