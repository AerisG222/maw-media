using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes;

namespace MawMedia.Services.Tests.Api;

public class PersonRoutesTests
    : ApiTestBase
{
    const string ROUTE_SYNC = "/api/v1/persons/sync";
    const string ROUTE_DELETIONS = "/api/v1/persons/deletions";

    public PersonRoutesTests(TestFixture fixture)
        : base(fixture)
    {

    }

    [Fact]
    public async Task SyncRequiresAuthentication()
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, new[] { NewPerson("api-anon") }, JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SyncRequiresTheFaceRecognitionPublishScope()
    {
        // authenticated as an admin, but without the scope the m2m app is granted
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, new[] { NewPerson("api-no-scope") }, JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SyncRejectsANonAdminHoldingTheScope()
    {
        // the scope gets past the endpoint policy; media.sync_persons then answers
        // forbidden because the media user is not an admin, and that is promoted
        // to a real 403 rather than a 200 whose body says forbidden
        using var client = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionPublish);

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, new[] { NewPerson("api-non-admin") }, JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SyncAppliesABatch()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionPublish);
        var token = TestContext.Current.CancellationToken;
        var personId = Guid.CreateVersion7();

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, new[] { NewPerson("api-applies", personId) }, JsonOptions, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<FaceSyncResult[]>(JsonOptions, token);

        var result = Assert.Single(results!);

        Assert.Equal("person", result.Entity);
        Assert.Equal(personId, result.EntityId);
        Assert.Equal("applied", result.Outcome);
    }

    [Fact]
    public async Task SyncRejectsABatchOverTheLimit()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionPublish);

        var tooMany = Enumerable
            .Range(0, PersonRoutes.MAX_PERSONS + 1)
            .Select(_ => NewPerson("api-too-many"))
            .ToArray();

        var response = await client.PostAsJsonAsync(
            ROUTE_SYNC, tooMany, JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeletionsAcceptAnIdArray()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionPublish);
        var token = TestContext.Current.CancellationToken;
        var missingId = Guid.CreateVersion7();

        var response = await client.PostAsJsonAsync(
            ROUTE_DELETIONS, new[] { missingId }, JsonOptions, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<FaceSyncResult[]>(JsonOptions, token);

        var result = Assert.Single(results!);

        Assert.Equal("not_found", result.Outcome);
    }

    static PersonSync NewPerson(string name, Guid? id = null) =>
        new(id ?? Guid.CreateVersion7(), name, null, null, null, 1, 1, null);
}
