using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models;
using MawMedia.Models.FaceRecognition;
using MawMedia.Routes;
using MawMedia.ViewModels;

namespace MawMedia.Services.Tests.Api;

public class PersonRoutesTests
    : ApiTestBase
{
    const string ROUTE_LIST = "/api/v1/persons";
    const string ROUTE_SYNC = "/api/v1/persons/sync";
    const string ROUTE_DELETIONS = "/api/v1/persons/deletions";

    static string MediaRoute(Guid personId) => $"{ROUTE_LIST}/{personId}/media";

    static string CategoryRoute(Guid personId) => $"{ROUTE_LIST}/{personId}/categories";

    static string FavoriteRoute(Guid personId) => $"{ROUTE_LIST}/{personId}/favorite";

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
    public async Task GetPersonsFlagsAndFiltersFavorites()
    {
        using var client = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var all = await client.GetFromJsonAsync<Person[]>(ROUTE_LIST, JsonOptions, token);

        Assert.True(all!.Single(p => p.Id == Constants.PERSON_SHARED).IsFavorite);

        var favorites = await client.GetFromJsonAsync<Person[]>($"{ROUTE_LIST}?f=true", JsonOptions, token);

        Assert.Contains(favorites!, p => p.Id == Constants.PERSON_SHARED);
        Assert.All(favorites!, p => Assert.True(p.IsFavorite));
    }

    [Fact]
    public async Task FavoritePersonRequiresTheFaceRecognitionReadScope()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.PutAsJsonAsync(
            FavoriteRoute(Constants.PERSON_TOGGLE), new FavoriteRequest(true), JsonOptions,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FavoritePersonIsNotFoundForAPersonTheCallerCannotSee()
    {
        using var client = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);

        var response = await client.PutAsJsonAsync(
            FavoriteRoute(Constants.PERSON_PRIVATE), new FavoriteRequest(true), JsonOptions,
            TestContext.Current.CancellationToken);

        // 404 rather than 403, so favouriting cannot be used to probe for people
        // the picker hid
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FavoritePersonRoundTrips()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;
        var route = FavoriteRoute(Constants.PERSON_TOGGLE);

        try
        {
            var on = await client.PutAsJsonAsync(route, new FavoriteRequest(true), JsonOptions, token);

            Assert.Equal(HttpStatusCode.OK, on.StatusCode);

            // the updated person comes back, so a client does not have to refetch
            // the list to redraw one row
            var favorited = await on.Content.ReadFromJsonAsync<Person>(JsonOptions, token);

            Assert.Equal(Constants.PERSON_TOGGLE, favorited!.Id);
            Assert.True(favorited.IsFavorite);
        }
        finally
        {
            var off = await client.PutAsJsonAsync(route, new FavoriteRequest(false), JsonOptions, token);

            var unfavorited = await off.Content.ReadFromJsonAsync<Person>(JsonOptions, token);

            Assert.False(unfavorited!.IsFavorite);
        }
    }

    [Fact]
    public async Task PersonCategoriesRequiresTheFaceRecognitionReadScope()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var response = await client.GetAsync(CategoryRoute(Constants.PERSON_SHARED), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PersonCategoriesReturnsOnlyCategoriesTheCallerCanSee()
    {
        var token = TestContext.Current.CancellationToken;

        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        using var friend = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);

        var forAdmin = await admin.GetFromJsonAsync<SearchResult<Category>>(
            CategoryRoute(Constants.PERSON_SHARED), JsonOptions, token);

        // PERSON_SHARED appears in a nature photo and the travel photo, so admin
        // gets both categories while the friend gets only the one they can reach
        Assert.Equal(2, forAdmin!.Results.Count());
        Assert.Contains(forAdmin.Results, c => c.Id == Constants.CATEGORY_NATURE.Id);
        Assert.Contains(forAdmin.Results, c => c.Id == Constants.CATEGORY_TRAVEL.Id);
        Assert.False(forAdmin.HasMoreResults);

        var forFriend = await friend.GetFromJsonAsync<SearchResult<Category>>(
            CategoryRoute(Constants.PERSON_SHARED), JsonOptions, token);

        var only = Assert.Single(forFriend!.Results);

        Assert.Equal(Constants.CATEGORY_TRAVEL.Id, only.Id);
    }

    [Fact]
    public async Task PersonCategoriesCarryTheCategoryTeaserAndAMediaCount()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var result = await client.GetFromJsonAsync<SearchResult<Category>>(
            CategoryRoute(Constants.PERSON_SHARED), JsonOptions, token);

        var travel = Assert.Single(result!.Results, c => c.Id == Constants.CATEGORY_TRAVEL.Id);

        // the category's own teaser, not a photo of the person - the tile has to
        // look like the same category everywhere else in the app
        Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, travel.Teaser.Id);
        Assert.NotEmpty(travel.Teaser.Files);

        // one visible travel photo holds this person
        Assert.Equal(1, travel.MediaCount);

        // nature is asserted on for its count only - CategoryRepositoryTests
        // reassigns that category's teaser in parallel, so which media answers
        // for it is not stable here
        var nature = Assert.Single(result.Results, c => c.Id == Constants.CATEGORY_NATURE.Id);

        Assert.Equal(1, nature.MediaCount);
    }

    [Fact]
    public async Task PersonCategoriesFavoritesCoverBothTheCategoryAndItsMedia()
    {
        var token = TestContext.Current.CancellationToken;

        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        using var friend = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);

        // admin favourited the travel *photo* and not the travel category, so the
        // category can only come back through the media half of the filter.
        //
        // only Contains, never a count: admin's nature favourites - the category
        // one and the media one - are both toggled by other test classes running
        // in parallel, so whether nature joins this result is not stable.
        var forAdmin = await admin.GetFromJsonAsync<SearchResult<Category>>(
            $"{CategoryRoute(Constants.PERSON_SHARED)}?f=true", JsonOptions, token);

        Assert.Contains(forAdmin!.Results, c => c.Id == Constants.CATEGORY_TRAVEL.Id);

        // the friend is the mirror image: they favourited the travel *category*
        // and none of the media in it, so this proves the category half carries
        // the filter on its own rather than riding on a favourited photo
        var forFriend = await friend.GetFromJsonAsync<SearchResult<Category>>(
            $"{CategoryRoute(Constants.PERSON_SHARED)}?f=true", JsonOptions, token);

        var only = Assert.Single(forFriend!.Results);

        Assert.Equal(Constants.CATEGORY_TRAVEL.Id, only.Id);

        // the count is the person's full visible total for the category, not the
        // favourited subset.  this friend favourited the category and none of
        // the photos in it, so a count that honoured the filter would say 0 here
        // - the filter picks the categories, the count reports photos.
        Assert.Equal(1, only.MediaCount);
    }

    [Fact]
    public async Task PersonCategoriesAreNotFoundForAPersonTheCallerCannotSee()
    {
        var token = TestContext.Current.CancellationToken;

        using var friend = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.FaceRecognitionRead);

        var response = await friend.GetAsync(CategoryRoute(Constants.PERSON_PRIVATE), token);

        // 404 rather than 403, matching the media view: the friend must not learn
        // this person exists
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);

        var forAdmin = await admin.GetAsync(CategoryRoute(Constants.PERSON_PRIVATE), token);

        Assert.Equal(HttpStatusCode.OK, forAdmin.StatusCode);
    }

    [Fact]
    public async Task PersonCategoriesAreNotFoundForAnUnknownPerson()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);

        var response = await client.GetAsync(
            CategoryRoute(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PersonCategoriesRejectANegativeOffset()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);

        var response = await client.GetAsync(
            $"{CategoryRoute(Constants.PERSON_SHARED)}?o=-1", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PersonCategoriesPastTheEndAreAnEmptyPageRatherThanNotFound()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.FaceRecognitionRead);
        var token = TestContext.Current.CancellationToken;

        var response = await client.GetAsync($"{CategoryRoute(Constants.PERSON_SHARED)}?o=100", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SearchResult<Category>>(JsonOptions, token);

        Assert.Empty(result!.Results);
        Assert.False(result.HasMoreResults);
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
