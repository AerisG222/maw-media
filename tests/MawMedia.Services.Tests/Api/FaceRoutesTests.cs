using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes;

namespace MawMedia.Services.Tests.Api;

public class FaceRoutesTests
    : ApiTestBase
{
    const string ROUTE_SYNC = "/api/v1/faces/sync";
    const string ROUTE_DELETIONS = "/api/v1/faces/deletions";

    public FaceRoutesTests(TestFixture fixture)
        : base(fixture)
    {

    }

    [Fact]
    public async Task SyncRequiresTheFaceRecognitionPublishScope()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, new[] { NewFace() }, JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SyncAppliesABatch()
    {
        using var client = Publisher();
        var token = TestContext.Current.CancellationToken;
        var faceId = Guid.CreateVersion7();

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, new[] { NewFace(faceId) }, JsonOptions, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = Assert.Single((await Results(response, token))!);

        Assert.Equal("face", result.Entity);
        Assert.Equal(faceId, result.EntityId);
        Assert.Equal("applied", result.Outcome);
    }

    [Fact]
    public async Task SyncReportsAnUnresolvedPathAsASuccessfulResponse()
    {
        using var client = Publisher();
        var token = TestContext.Current.CancellationToken;
        var faceId = Guid.CreateVersion7();
        const string missingPath = "/media/api-no-such-file.jpg";

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, new[] { NewFace(faceId, missingPath) }, JsonOptions, token);

        // a per item problem is not a failed request - the publisher needs the
        // rest of the batch to have landed, and the detail to act on
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = Assert.Single((await Results(response, token))!);

        Assert.Equal("unresolved_path", result.Outcome);
        Assert.Equal(missingPath, result.Detail);
    }

    [Fact]
    public async Task SyncRejectsABatchOverTheLimit()
    {
        using var client = Publisher();

        var tooMany = Enumerable
            .Range(0, FaceRoutes.MAX_FACES + 1)
            .Select(_ => NewFace())
            .ToArray();

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, tooMany, JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeletionsRemoveAPublishedFace()
    {
        using var client = Publisher();
        var token = TestContext.Current.CancellationToken;
        var faceId = Guid.CreateVersion7();

        await client.PostAsJsonAsync(ROUTE_SYNC, new[] { NewFace(faceId) }, JsonOptions, token);

        var response = await client.PostAsJsonAsync(
            ROUTE_DELETIONS, new[] { faceId }, JsonOptions, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = Assert.Single((await Results(response, token))!);

        Assert.Equal("deleted", result.Outcome);
    }

    HttpClient Publisher() =>
        Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionPublish);

    static async Task<FaceSyncResult[]?> Results(HttpResponseMessage response, CancellationToken token) =>
        await response.Content.ReadFromJsonAsync<FaceSyncResult[]>(JsonOptions, token);

    static FaceSync NewFace(Guid? id = null, string? path = null) =>
        new(
            id ?? Guid.CreateVersion7(),
            path ?? Constants.FILE_NATURE_1.Path,
            null,
            0.1m,
            0.2m,
            0.3m,
            0.4m,
            0.9f,
            1
        );
}
