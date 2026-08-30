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

    // the fixtures derive two countries:
    //
    //   USA            -> NY      -> New York  (MEDIA_TRAVEL_1 etc)
    //                  -> MA      -> Boston    (MEDIA_PLACE_MA, MEDIA_PLACE_OVERRIDE)
    //   United Kingdom -> England -> London    (MEDIA_PLACE_UK, admin only)
    //
    // resolving them by drilling rather than by hardcoding ids means these tests
    // also assert the drill-down works as a side effect of setting themselves up.
    async Task<Place> Country(HttpClient client, string slug)
    {
        var countries = await client.GetFromJsonAsync<Place[]>(
            ROUTE_LIST, JsonOptions, TestContext.Current.CancellationToken);

        return Assert.Single(countries!, c => c.Slug == slug);
    }

    async Task<Place> Child(HttpClient client, Place parent, string slug)
    {
        var children = await client.GetFromJsonAsync<Place[]>(
            $"{ROUTE_LIST}?parent={parent.Id}", JsonOptions, TestContext.Current.CancellationToken);

        return Assert.Single(children!, c => c.Slug == slug);
    }

    async Task<(Place Country, Place State, Place City)> Usa(HttpClient client, string stateSlug, string citySlug)
    {
        var country = await Country(client, "usa");
        var state = await Child(client, country, stateSlug);
        var city = await Child(client, state, citySlug);

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

        var (country, state, city) = await Usa(client, "ny", "new-york");

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

        var (country, ny, newYork) = await Usa(client, "ny", "new-york");
        var (_, ma, boston) = await Usa(client, "ma", "boston");

        // a state with one city mirrors it, and the country is the sum of both
        // branches - a count that only covered coordinates filed directly against
        // the country would read zero here
        Assert.Equal(newYork.MediaCount, ny.MediaCount);
        Assert.Equal(boston.MediaCount, ma.MediaCount);
        Assert.Equal(ny.MediaCount + ma.MediaCount, country.MediaCount);

        // and none may be zero: a place with nothing visible is supposed to be
        // absent rather than listed empty
        Assert.True(newYork.MediaCount > 0);
        Assert.True(boston.MediaCount > 0);
    }

    [Fact]
    public async Task GetPlacesNarrowsByKind()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var country = await Country(client, "usa");

        var states = await client.GetFromJsonAsync<Place[]>(
            $"{ROUTE_LIST}?parent={country.Id}&kind=state", JsonOptions, token);
        var cities = await client.GetFromJsonAsync<Place[]>(
            $"{ROUTE_LIST}?parent={country.Id}&kind=city", JsonOptions, token);

        Assert.Equal(2, states!.Length);
        Assert.Empty(cities!);
    }

    [Fact]
    public async Task GetPlaceReturnsASinglePlaceAndNotFoundForAnUnknownId()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await Usa(client, "ny", "new-york");

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

        var (country, state, city) = await Usa(client, "ny", "new-york");

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

        var (_, _, city) = await Usa(client, "ny", "new-york");

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

        var (_, _, city) = await Usa(client, "ny", "new-york");

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

        var (_, _, city) = await Usa(client, "ny", "new-york");

        var media = await client.GetAsync($"{MediaRoute(city.Id)}?o=-1", token);
        var categories = await client.GetAsync($"{CategoryRoute(city.Id)}?o=-1", token);

        Assert.Equal(HttpStatusCode.BadRequest, media.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, categories.StatusCode);
    }

    [Fact]
    public async Task AnUngeocodedLocationIsNotBrowsable()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);

        // LOCATION_UNK has no country, so it resolves to no place at all.  the
        // listing must therefore be exactly the two derived countries, with no
        // third bucket standing in for "somewhere".
        var countries = await client.GetFromJsonAsync<Place[]>(
            ROUTE_LIST, JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(["united-kingdom", "usa"], countries!.Select(c => c.Slug).Order());
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

    [Fact]
    public async Task APlaceIsAbsentEntirelyWhenNoneOfItsMediaAreVisible()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var friend = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        // LOCATION_UK is reachable only through CATEGORY_FOOD, which ROLE_FRIEND
        // does not hold.  so the United Kingdom must not merely report zero for
        // johndoe - it must not be in his listing at all, or its existence leaks.
        var uk = await Country(admin, "united-kingdom");

        var friendCountries = await friend.GetFromJsonAsync<Place[]>(ROUTE_LIST, JsonOptions, token);

        Assert.DoesNotContain(friendCountries!, c => c.Id == uk.Id);
        Assert.Contains(friendCountries!, c => c.Slug == "usa");

        // and the drill-ins answer 404 rather than an empty page, so he cannot
        // confirm the place exists by asking about it directly
        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync(PlaceRoute(uk.Id), token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync(MediaRoute(uk.Id), token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync(CategoryRoute(uk.Id), token)).StatusCode);
    }

    [Fact]
    public async Task AnOverriddenLocationBrowsesUnderTheOverrideNotTheRecordedOne()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        // MEDIA_PLACE_OVERRIDE is recorded at LOCATION_NY and overridden to
        // LOCATION_MA.  this is not a corner case: the phase 0 audit found 60,472
        // production media reachable only through the override column, and 1,471
        // carrying both with different values.
        var (_, _, newYork) = await Usa(client, "ny", "new-york");
        var (_, _, boston) = await Usa(client, "ma", "boston");

        var inBoston = await client.GetFromJsonAsync<SearchResult<Media>>(MediaRoute(boston.Id), JsonOptions, token);
        var inNewYork = await client.GetFromJsonAsync<SearchResult<Media>>(MediaRoute(newYork.Id), JsonOptions, token);

        Assert.Contains(inBoston!.Results, m => m.Id == Constants.MEDIA_PLACE_OVERRIDE.Id);
        Assert.DoesNotContain(inNewYork!.Results, m => m.Id == Constants.MEDIA_PLACE_OVERRIDE.Id);

        // the media recorded at LOCATION_MA with no override sits beside it, so
        // both routes into a place agree
        Assert.Contains(inBoston.Results, m => m.Id == Constants.MEDIA_PLACE_MA.Id);
    }

    [Fact]
    public async Task DrillingIntoACountryIncludesEveryDescendantsMedia()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var country = await Country(client, "usa");

        // place_id records only the deepest place a coordinate resolved to, so a
        // country query that did not expand its subtree would return nothing at
        // all here - every fixture is filed against a city
        var result = await client.GetFromJsonAsync<SearchResult<Media>>(MediaRoute(country.Id), JsonOptions, token);

        Assert.Contains(result!.Results, m => m.Id == Constants.MEDIA_PLACE_MA.Id);
        Assert.Contains(result.Results, m => m.Id == Constants.MEDIA_TRAVEL_1.Id);
    }
}
