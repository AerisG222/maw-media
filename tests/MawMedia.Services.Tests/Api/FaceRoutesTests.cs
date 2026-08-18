using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes;
using MawMedia.Services;

namespace MawMedia.Services.Tests.Api;

public class FaceRoutesTests
    : ApiTestBase
{
    static readonly byte[] AVIF_HEADER =
        [0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x66];

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

    [Fact]
    public async Task PutImageRequiresTheFaceRecognitionPublishScope()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.PutAsync(
            ImageRoute(Guid.CreateVersion7()), Avif(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutImageIsNotFoundForAFaceThatWasNeverPublished()
    {
        using var client = Publisher();

        var response = await client.PutAsync(
            ImageRoute(Guid.CreateVersion7()), Avif(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutImageRejectsAnUnexpectedContentType()
    {
        using var client = Publisher();
        var token = TestContext.Current.CancellationToken;
        var faceId = await PublishFace(client, token);

        var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new("image/jpeg");

        var response = await client.PutAsync(ImageRoute(faceId), content, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutImageRejectsAnOversizeBody()
    {
        using var client = Publisher();
        var token = TestContext.Current.CancellationToken;
        var faceId = await PublishFace(client, token);

        var response = await client.PutAsync(
            ImageRoute(faceId), Avif(FaceRoutes.MAX_IMAGE_BYTES + 1), token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutImageStoresBytesThatCanBeReadBack()
    {
        using var client = Publisher();
        var token = TestContext.Current.CancellationToken;
        var faceId = await PublishFace(client, token);
        var bytes = (byte[])[.. AVIF_HEADER, 1, 2, 3, 4];

        var put = await client.PutAsync(ImageRoute(faceId), Avif(bytes), token);

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        // read back as any authenticated user - the publish scope is not needed
        using var reader = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.MediaRead);

        var get = await reader.GetAsync(ImageRoute(faceId), token);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(FaceImageStore.CONTENT_TYPE, get.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await get.Content.ReadAsByteArrayAsync(token));

        // republishing replaces rather than appends
        var replacement = (byte[])[.. AVIF_HEADER, 9, 9];

        await client.PutAsync(ImageRoute(faceId), Avif(replacement), token);

        var again = await reader.GetAsync(ImageRoute(faceId), token);

        Assert.Equal(replacement, await again.Content.ReadAsByteArrayAsync(token));
    }

    [Fact]
    public async Task GetImageIsNotFoundWhenNothingWasPublished()
    {
        using var client = Publisher();
        var token = TestContext.Current.CancellationToken;
        var faceId = await PublishFace(client, token);

        var response = await client.GetAsync(ImageRoute(faceId), token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    static string ImageRoute(Guid faceId) => $"/api/v1/faces/{faceId}/image";

    static ByteArrayContent Avif(int size) => Avif(new byte[size]);

    // the bytes are never decoded by the api, so these only need to be
    // recognisable in a failure message - this is an avif ftyp box header
    static ByteArrayContent Avif(byte[]? bytes = null)
    {
        var content = new ByteArrayContent(bytes ?? AVIF_HEADER);

        content.Headers.ContentType = new(FaceImageStore.CONTENT_TYPE);

        return content;
    }

    async Task<Guid> PublishFace(HttpClient client, CancellationToken token)
    {
        var faceId = Guid.CreateVersion7();

        var response = await client.PostAsJsonAsync(ROUTE_SYNC, new[] { NewFace(faceId) }, JsonOptions, token);

        response.EnsureSuccessStatusCode();

        return faceId;
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
