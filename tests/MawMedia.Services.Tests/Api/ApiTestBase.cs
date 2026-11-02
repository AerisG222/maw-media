using System.Text.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace MawMedia.Services.Tests.Api;

public abstract class ApiTestBase
    : IDisposable
{
    // the api speaks camelCase like every other endpoint; NodaTime is needed for
    // PersonSync.SourceModified
    protected static readonly JsonSerializerOptions JsonOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    protected ApiFactory Factory { get; }

    protected ApiTestBase(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        Factory = new ApiFactory(fixture.ConnectionString);
    }

    public void Dispose()
    {
        Factory.Dispose();

        GC.SuppressFinalize(this);
    }

    // sub resolves against the seeded media.external_identity rows, so the real
    // claims transformation attaches the media user id and admin flag
    protected HttpClient Client(string sub, string scope)
    {
        var client = Factory.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.HEADER_SUB, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.HEADER_SCOPES, ApiFactory.QualifiedScope(scope));

        return client;
    }

    // the cookie a browser would hold after trading a token with this scope for it
    protected async Task<string> AssetCookie(string sub, string scope)
    {
        using var client = Client(sub, scope);

        var response = await client.PostAsync(
            new Uri("/api/v1/auth/asset-session", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }

    // no token, so the cookie is the only credential on the request
    protected async Task<HttpResponseMessage> SendWithCookie(HttpMethod method, Uri uri, string cookie)
    {
        using var anonymous = Factory.CreateClient();
        using var request = new HttpRequestMessage(method, uri);

        request.Headers.Add("Cookie", cookie);

        return await anonymous.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
