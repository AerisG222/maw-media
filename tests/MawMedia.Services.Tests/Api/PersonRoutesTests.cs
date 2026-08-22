using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes;

namespace MawMedia.Services.Tests.Api;

public class PersonRoutesTests
    : ApiTestBase
{
    const string ROUTE_LIST = "/api/v1/persons";
    const string ROUTE_SYNC = "/api/v1/persons/sync";
    const string ROUTE_DELETIONS = "/api/v1/persons/deletions";

    static string MediaRoute(Guid personId) => $"{ROUTE_LIST}/{personId}/media";

    public PersonRoutesTests(TestFixture fixture)
        : base(fixture)
    {

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
        Assert.EndsWith($"/assets/faces/{Constants.FACE_SHARED_TRAVEL}.avif", shared.PreferredFaceUrl);

        // the per caller count, never the published global face_count of 99
        Assert.Equal(2, shared.MediaCount);
    }

    [Fact]
    public async Task PersonMediaRequiresTheFaceRecognitionReadScope()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.GetAsync(MediaRoute(Constants.PERSON_SHARED), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PersonMediaReturnsOnlyMediaTheCallerCanSee()
    {
        using var client = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var result = await client.GetFromJsonAsync<SearchResult<Media>>(
            MediaRoute(Constants.PERSON_SHARED), JsonOptions, token);

        var media = Assert.Single(result!.Results);

        Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, media.Id);
        Assert.False(result.HasMoreResults);
    }

    [Fact]
    public async Task PersonMediaIsNotFoundForAPersonTheCallerCannotSee()
    {
        using var client = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);

        var response = await client.GetAsync(
            MediaRoute(Constants.PERSON_PRIVATE), TestContext.Current.CancellationToken);

        // 404 rather than 403: the friend must not learn this person exists,
        // which is the same reason the face image download hides behind 404
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);

        var forAdmin = await admin.GetAsync(
            MediaRoute(Constants.PERSON_PRIVATE), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, forAdmin.StatusCode);
    }

    [Fact]
    public async Task PersonMediaIsNotFoundForAnUnknownPerson()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);

        var response = await client.GetAsync(
            MediaRoute(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PersonMediaRejectsANegativeOffset()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);

        var response = await client.GetAsync(
            $"{MediaRoute(Constants.PERSON_SHARED)}?o=-1", TestContext.Current.CancellationToken);

        // caught at the route so the repository's guard clause never surfaces as
        // a 500
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PersonMediaCanBeFilteredToFavorites()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var result = await client.GetFromJsonAsync<SearchResult<Media>>(
            $"{MediaRoute(Constants.PERSON_SHARED)}?f=true", JsonOptions, token);

        var media = Assert.Single(result!.Results);

        Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, media.Id);
    }

    [Fact]
    public async Task PersonMediaWithNoFavoritesIsAnEmptyPageRatherThanNotFound()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var response = await client.GetAsync($"{MediaRoute(Constants.PERSON_PRIVATE)}?f=true", token);

        // the person is visible and simply has no favourites - answering 404
        // would make an empty filter look like a broken link
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SearchResult<Media>>(JsonOptions, token);

        Assert.Empty(result!.Results);
    }

    [Fact]
    public async Task PersonMediaAcceptsASeedForAShuffledOrder()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var first = await client.GetFromJsonAsync<SearchResult<Media>>(
            $"{MediaRoute(Constants.PERSON_SHARED)}?seed=42", JsonOptions, token);

        var again = await client.GetFromJsonAsync<SearchResult<Media>>(
            $"{MediaRoute(Constants.PERSON_SHARED)}?seed=42", JsonOptions, token);

        Assert.Equal(2, first!.Results.Count());
        Assert.Equal(
            first.Results.Select(m => m.Id).ToList(),
            again!.Results.Select(m => m.Id).ToList()
        );
    }

    [Fact]
    public async Task PersonMediaReturnsAnEmptyPagePastTheEnd()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var response = await client.GetAsync($"{MediaRoute(Constants.PERSON_SHARED)}?o=100", token);

        // an exhausted page is a normal answer, not a missing person - only the
        // first page collapses to 404
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SearchResult<Media>>(JsonOptions, token);

        Assert.Empty(result!.Results);
        Assert.False(result.HasMoreResults);
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
