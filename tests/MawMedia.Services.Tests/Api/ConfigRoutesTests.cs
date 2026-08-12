using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models.FaceRecognition;

namespace MawMedia.Services.Tests.Api;

public class ConfigRoutesTests
    : ApiTestBase
{
    const string ROUTE_SYNC = "/api/v1/config/person-statuses/sync";

    public ConfigRoutesTests(TestFixture fixture)
        : base(fixture)
    {

    }

    [Fact]
    public async Task PersonStatusSyncRequiresTheFaceRecognitionPublishScope()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, new[] { NewStatus("api-no-scope") }, JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PersonStatusSyncAppliesABatch()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionPublish);
        var token = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, new[] { NewStatus("api-applied") }, JsonOptions, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<FaceSyncResult[]>(JsonOptions, token);
        var result = Assert.Single(results!);

        Assert.Equal("status", result.Entity);
        Assert.Equal("applied", result.Outcome);

        // the code travels in detail, since a status has no uuid
        Assert.Equal("api-applied", result.Detail);
    }

    static PersonStatusSync NewStatus(string code) =>
        new(code, "Api Test", "seeded by an api test", 9);
}
