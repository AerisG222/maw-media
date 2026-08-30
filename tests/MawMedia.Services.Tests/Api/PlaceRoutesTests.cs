using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models;

namespace MawMedia.Services.Tests.Api;

public class PlaceRoutesTests
    : ApiTestBase
{
    const string ROUTE_LIST = "/api/v1/places";

    static string PlaceRoute(Guid id) => $"{ROUTE_LIST}/{id}";
    static string AncestorsRoute(Guid id) => $"{ROUTE_LIST}/{id}/ancestors";
    static string MediaRoute(Guid id) => $"{ROUTE_LIST}/{id}/media";
    static string CategoryRoute(Guid id) => $"{ROUTE_LIST}/{id}/categories";

    public PlaceRoutesTests(TestFixture fixture)
        : base(fixture)
    {

    }

    // the fixtures put every media at LOCATION_NY, so the derived tree is
    // USA > NY > New York.  resolving it by drilling rather than by hardcoding ids
    // means these tests also assert that the drill-down itself works.
    async Task<(Place Country, Place State, Place City)> ResolveTree(HttpClient client)
    {
        var token = TestContext.Current.CancellationToken;

        var countries = await client.GetFromJsonAsync<Place[]>(ROUTE_LIST, JsonOptions, token);
        var country = Assert.Single(countries!);

        var states = await client.GetFromJsonAsync<Place[]>($"{ROUTE_LIST}?parent={country.Id}", JsonOptions, token);
        var state = Assert.Single(states!);

        var cities = await client.GetFromJsonAsync<Place[]>($"{ROUTE_LIST}?parent={state.Id}", JsonOptions, token);
        var city = Assert.Single(cities!);

        return (country, state, city);
    }

    [Fact]
    public async Task GetPlacesRequiresTheMediaReadScope()
    {
        // the geocode maintenance scope must not open the browse, or a browse
        // client would be handed the correction worker's permissions
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationRead);

        var response = await client.GetAsync(ROUTE_LIST, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPlacesReturnsTheDerivedHierarchy()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var (country, state, city) = await ResolveTree(client);

        Assert.Equal("country", country.Kind);
        Assert.Equal("state", state.Kind);
        Assert.Equal("city", city.Kind);

        Assert.Equal("USA", country.Name);
        Assert.Equal("NY", state.Name);
        Assert.Equal("New York", city.Name);

        // parentage is what a client drills by, so it has to be returned correctly
        Assert.Null(country.ParentId);
        Assert.Equal(country.Id, state.ParentId);
        Assert.Equal(state.Id, city.ParentId);
    }

    [Fact]
    public async Task PlaceCountsRollUpThroughTheHierarchy()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        var (country, state, city) = await ResolveTree(client);

        // every fixture media sits at the one city, so all three levels agree -
        // and none of them may be zero, since a place with nothing visible is
        // supposed to be absent rather than listed empty
        Assert.True(city.MediaCount > 0);
        Assert.Equal(city.MediaCount, state.MediaCount);
        Assert.Equal(state.MediaCount, country.MediaCount);
    }

    [Fact]
    public async Task GetPlacesNarrowsByKind()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (country, _, _) = await ResolveTree(client);

        var states = await client.GetFromJsonAsync<Place[]>(
            $"{ROUTE_LIST}?parent={country.Id}&kind=state", JsonOptions, token);
        var cities = await client.GetFromJsonAsync<Place[]>(
            $"{ROUTE_LIST}?parent={country.Id}&kind=city", JsonOptions, token);

        Assert.Single(states!);
        Assert.Empty(cities!);
    }

    [Fact]
    public async Task GetPlaceReturnsASinglePlaceAndNotFoundForAnUnknownId()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await ResolveTree(client);

        var fetched = await client.GetFromJsonAsync<Place>(PlaceRoute(city.Id), JsonOptions, token);

        Assert.Equal(city.Id, fetched!.Id);
        Assert.Equal(city.MediaCount, fetched.MediaCount);

        // 404 rather than 403, so a caller cannot probe for places hidden from them
        var missing = await client.GetAsync(PlaceRoute(Guid.CreateVersion7()), token);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task GetPlaceAncestorsReturnsTheBreadcrumbRootFirst()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (country, state, city) = await ResolveTree(client);

        var chain = await client.GetFromJsonAsync<PlaceAncestor[]>(AncestorsRoute(city.Id), JsonOptions, token);

        Assert.Equal(3, chain!.Length);
        Assert.Equal([country.Id, state.Id, city.Id], chain.Select(a => a.Id));
        Assert.Equal([1, 2, 3], chain.Select(a => a.Depth));

        // a country is its own breadcrumb rather than an empty one
        var root = await client.GetFromJsonAsync<PlaceAncestor[]>(AncestorsRoute(country.Id), JsonOptions, token);

        Assert.Equal(country.Id, Assert.Single(root!).Id);
    }

    [Fact]
    public async Task GetPlaceMediaReturnsMediaWithAbsoluteFileUrls()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await ResolveTree(client);

        var result = await client.GetFromJsonAsync<SearchResult<Media>>(MediaRoute(city.Id), JsonOptions, token);

        Assert.NotEmpty(result!.Results);

        // clients should not have to assemble these
        var file = result.Results.First().Files.First();

        Assert.StartsWith("http", file.Path);
    }

    [Fact]
    public async Task GetPlaceCategoriesCarriesAPerPlaceMediaCount()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await ResolveTree(client);

        var result = await client.GetFromJsonAsync<SearchResult<Category>>(CategoryRoute(city.Id), JsonOptions, token);

        Assert.NotEmpty(result!.Results);

        // MediaCount is null on the plain category grid and populated here; it can
        // never be zero, because a category only appears when a media put it there
        Assert.All(result.Results, c =>
        {
            Assert.NotNull(c.MediaCount);
            Assert.True(c.MediaCount > 0);
        });
    }

    [Fact]
    public async Task PlaceDrillInsReturnNotFoundForAnUnknownPlace()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var unknown = Guid.CreateVersion7();

        var media = await client.GetAsync(MediaRoute(unknown), token);
        var categories = await client.GetAsync(CategoryRoute(unknown), token);
        var ancestors = await client.GetAsync(AncestorsRoute(unknown), token);

        Assert.Equal(HttpStatusCode.NotFound, media.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, categories.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ancestors.StatusCode);
    }

    [Fact]
    public async Task ANegativeOffsetIsRejected()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await ResolveTree(client);

        var media = await client.GetAsync($"{MediaRoute(city.Id)}?o=-1", token);
        var categories = await client.GetAsync($"{CategoryRoute(city.Id)}?o=-1", token);

        Assert.Equal(HttpStatusCode.BadRequest, media.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, categories.StatusCode);
    }

    [Fact]
    public async Task AnUngeocodedLocationIsNotBrowsable()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        // LOCATION_UNK has no country, so it resolves to no place at all - the
        // whole tree must be the one derived from LOCATION_NY
        var countries = await client.GetFromJsonAsync<Place[]>(
            ROUTE_LIST, JsonOptions, TestContext.Current.CancellationToken);

        Assert.Single(countries!);
    }

    [Fact]
    public async Task PlacesAreScopedToWhatTheCallerCanSee()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var friend = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var forAdmin = await admin.GetFromJsonAsync<Place[]>(ROUTE_LIST, JsonOptions, token);
        var forFriend = await friend.GetFromJsonAsync<Place[]>(ROUTE_LIST, JsonOptions, token);

        // the restricted user reaches fewer categories, so the same place must
        // report a smaller count - never a larger one, and never an extra place
        Assert.All(forFriend!, f =>
        {
            var match = Assert.Single(forAdmin!, a => a.Id == f.Id);

            Assert.True(f.MediaCount <= match.MediaCount);
        });
    }
}
