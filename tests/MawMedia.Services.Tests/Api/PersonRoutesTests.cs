using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes;

namespace MawMedia.Services.Tests.Api;

public class PersonRoutesTests
    : ApiTestBase
{
    const string ROUTE_LIST = "/api/v1/persons";
    const string ROUTE_SYNC = "/api/v1/persons/sync";
    const string ROUTE_DELETIONS = "/api/v1/persons/deletions";

    public PersonRoutesTests(TestFixture fixture)
        : base(fixture)
    {

    }

    [Fact]
    public async Task ListRequiresTheFaceRecognitionReadScope()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.GetAsync(ROUTE_LIST, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListReturnsOnlyPeopleTheCallerCanSee()
    {
        using var client = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var response = await client.GetAsync(ROUTE_LIST, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var people = await response.Content.ReadFromJsonAsync<Person[]>(JsonOptions, token);

        Assert.Contains(people!, p => p.Id == Constants.PERSON_SHARED);
        Assert.DoesNotContain(people!, p => p.Id == Constants.PERSON_PRIVATE);
    }

    [Fact]
    public async Task ListReturnsAnAbsolutePreferredFaceUrl()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var response = await client.GetAsync(ROUTE_LIST, token);
        var people = await response.Content.ReadFromJsonAsync<Person[]>(JsonOptions, token);
        var shared = people!.Single(p => p.Id == Constants.PERSON_SHARED);

        // absolute so clients do not each assemble it; the route version is part
        // of the shape, so a v2 move surfaces here
        Assert.NotNull(shared.PreferredFaceUrl);
        Assert.EndsWith($"/api/v1/faces/{Constants.FACE_SHARED_TRAVEL}/image", shared.PreferredFaceUrl);
        Assert.StartsWith("http", shared.PreferredFaceUrl);

        // never the published global figure, which the seed sets to 99
        Assert.Equal(2, shared.MediaCount);
    }

    [Fact]
    public async Task GetPersonsRequiresTheFaceRecognitionReadScope()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.GetAsync(ROUTE_LIST, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPersonsReturnsOnlyPeopleTheCallerCanSee()
    {
        var token = TestContext.Current.CancellationToken;

        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        using var friend = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);

        var forAdmin = await admin.GetFromJsonAsync<Person[]>(ROUTE_LIST, JsonOptions, token);
        var forFriend = await friend.GetFromJsonAsync<Person[]>(ROUTE_LIST, JsonOptions, token);

        Assert.Contains(forAdmin!, p => p.Id == Constants.PERSON_PRIVATE);

        // the private person appears only in a category ROLE_FRIEND cannot reach
        Assert.DoesNotContain(forFriend!, p => p.Id == Constants.PERSON_PRIVATE);
        Assert.Contains(forFriend!, p => p.Id == Constants.PERSON_SHARED);
    }

    [Fact]
    public async Task GetPersonsReturnsAnAbsolutePreferredFaceUrl()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);

        var people = await client.GetFromJsonAsync<Person[]>(
            ROUTE_LIST, JsonOptions, TestContext.Current.CancellationToken);

        var shared = Assert.Single(people!, p => p.Id == Constants.PERSON_SHARED);

        // clients should not have to assemble this, and it must be reachable
        Assert.NotNull(shared.PreferredFaceUrl);
        Assert.EndsWith($"/api/v1/faces/{Constants.FACE_SHARED_TRAVEL}/image", shared.PreferredFaceUrl);

        // the per caller count, never the published global face_count of 99
        Assert.Equal(2, shared.MediaCount);
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
