using System.Net;
using System.Net.Http.Json;
using MawMedia;
using MawMedia.Models;
using MawMedia.ViewModels;

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

    static string CoverRoute(Guid id) => $"{ROUTE_LIST}/{id}/cover";

    [Fact]
    public async Task SettingACoverRequiresTheLocationWriteScope()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await Usa(client, "ny", "new-york");

        var response = await client.PutAsJsonAsync(
            CoverRoute(city.Id), new PlaceCoverRequest(Constants.MEDIA_TRAVEL_1.Id), JsonOptions, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ANonAdminCannotSetACoverEvenWithTheScope()
    {
        using var browse = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await Usa(browse, "ny", "new-york");

        // choosing a cover publishes a photograph outside the authorization
        // boundary, so holding the scope is not enough - the database checks admin
        var response = await writer.PutAsJsonAsync(
            CoverRoute(city.Id), new PlaceCoverRequest(Constants.MEDIA_TRAVEL_1.Id), JsonOptions, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ACoverIsPublishedAndServedToAnySignedInCaller()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await Usa(admin, "ny", "new-york");

        Assert.Null(city.CoverUrl);

        var set = await writer.PutAsJsonAsync(
            CoverRoute(city.Id), new PlaceCoverRequest(Constants.MEDIA_TRAVEL_1.Id), JsonOptions, token);

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        var updated = await set.Content.ReadFromJsonAsync<Place>(JsonOptions, token);

        Assert.NotNull(updated!.CoverUrl);
        Assert.Contains("/assets/covers/", updated.CoverUrl);

        // the file is named for the place, so the url is derivable from the row -
        // no second id, and no lookup to answer "where is this image"
        Assert.Contains($"{city.Id}.avif", updated.CoverUrl);

        var image = await admin.GetAsync(new Uri(updated.CoverUrl), token);

        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/avif", image.Content.Headers.ContentType?.MediaType);
        Assert.True((await image.Content.ReadAsByteArrayAsync(token)).Length > 0);

        // immutable, which is what makes replacing a cover safe to cache forever,
        // and private so a shared cache cannot hand it to a signed out visitor
        var cacheControl = image.Headers.CacheControl?.ToString() ?? "";

        Assert.Contains("immutable", cacheControl);
        Assert.Contains("private", cacheControl);

        // signing out closes it: a cover is not public, it merely skips the per
        // file check that the rest of /assets applies
        using var anonymous = Factory.CreateClient();

        var refused = await anonymous.GetAsync(new Uri(updated.CoverUrl), token);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        await writer.DeleteAsync(CoverRoute(city.Id), token);
    }

    [Fact]
    public async Task TheCoverBranchOpensNoDoorIntoTheAssetTree()
    {
        using var anonymous = Factory.CreateClient();
        using var signedIn = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        // anything that is not "{guid}.avif" under the prefix is refused outright
        foreach (var probe in new[]
        {
            "/assets/covers/nature1.jpg",
            "/assets/covers/not-a-guid.avif",
            "/assets/covers/nested/path.avif",
            $"/assets/covers/{Guid.CreateVersion7()}.avif"
        })
        {
            var response = await signedIn.GetAsync(probe, token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // a traversal out of the public prefix is normalized by the client before
        // it is sent, so it arrives as a plain /assets request - which must still
        // be challenged.  the point is that it is never *served*, whichever branch
        // ends up judging it.
        foreach (var probe in new[]
        {
            "/assets/covers/../media/nature1.jpg",
            "/assets/media/nature1.jpg",
            "/assets/media/anything.avif"
        })
        {
            // a signed in caller who cannot see the media must still be refused -
            // the cover branch skips the per file check, the media branch does not
            var response = await signedIn.GetAsync(probe, token);

            Assert.True(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                $"{probe} answered {response.StatusCode}");
        }

        // and an anonymous caller is refused everywhere, covers included
        foreach (var probe in new[] { "/assets/media/nature1.jpg", $"/assets/covers/{Guid.CreateVersion7()}.avif" })
        {
            var response = await anonymous.GetAsync(probe, token);

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task ACoverMustComeFromMediaAtThatPlace()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var (_, _, boston) = await Usa(admin, "ma", "boston");

        // MEDIA_TRAVEL_1 is in New York, not Boston
        var response = await writer.PutAsJsonAsync(
            CoverRoute(boston.Id), new PlaceCoverRequest(Constants.MEDIA_TRAVEL_1.Id), JsonOptions, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ACountryMayBeRepresentedByAPhotographFromOneOfItsCities()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var country = await Country(admin, "usa");

        // the check is "at this place or beneath it", so a city photo represents
        // its country - which is what an admin actually wants
        var response = await writer.PutAsJsonAsync(
            CoverRoute(country.Id), new PlaceCoverRequest(Constants.MEDIA_TRAVEL_1.Id), JsonOptions, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await writer.DeleteAsync(CoverRoute(country.Id), token);
    }

    [Fact]
    public async Task ReplacingACoverMovesItToANewUrlAndClearingRemovesIt()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var (_, _, boston) = await Usa(admin, "ma", "boston");

        var first = await (await writer.PutAsJsonAsync(
            CoverRoute(boston.Id), new PlaceCoverRequest(Constants.MEDIA_PLACE_MA.Id), JsonOptions, token))
            .Content.ReadFromJsonAsync<Place>(JsonOptions, token);

        var second = await (await writer.PutAsJsonAsync(
            CoverRoute(boston.Id), new PlaceCoverRequest(Constants.MEDIA_PLACE_OVERRIDE.Id), JsonOptions, token))
            .Content.ReadFromJsonAsync<Place>(JsonOptions, token);

        // one file per place, so the path is unchanged - but the ?v= moves, which
        // is what stops a cache serving the image that was just replaced
        Assert.NotEqual(first!.CoverUrl, second!.CoverUrl);
        Assert.Equal(
            new Uri(first.CoverUrl!).AbsolutePath,
            new Uri(second.CoverUrl!).AbsolutePath);
        Assert.Contains($"{boston.Id}.avif", second.CoverUrl!);

        // and the bytes at that one path are now the replacement's
        var served = await admin.GetAsync(new Uri(second.CoverUrl!), token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(
            ApiFactory.StubRendition(Constants.FILE_PLACE_OVERRIDE_COVER.Path),
            await served.Content.ReadAsByteArrayAsync(token));

        var cleared = await (await writer.DeleteAsync(CoverRoute(boston.Id), token))
            .Content.ReadFromJsonAsync<Place>(JsonOptions, token);

        Assert.Null(cleared!.CoverUrl);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync(new Uri(second.CoverUrl!), token)).StatusCode);
    }

    [Fact]
    public async Task ACoverSkipsThePerFileCheckThatGovernsTheRestOfAssets()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        using var friend = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await Usa(admin, "ny", "new-york");

        // MEDIA_NATURE_1 lives in CATEGORY_NATURE, which ROLE_FRIEND cannot reach.
        // johndoe can see the place - MEDIA_TRAVEL_1 is there too - but not this
        // particular photograph.
        var set = await writer.PutAsJsonAsync(
            CoverRoute(city.Id), new PlaceCoverRequest(Constants.MEDIA_NATURE_1.Id), JsonOptions, token);

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        var updated = await set.Content.ReadFromJsonAsync<Place>(JsonOptions, token);

        try
        {
            // the underlying photograph stays closed to him...
            // the stored path is already a url under /assets
            var original = await friend.GetAsync(Constants.FILE_NATURE_1.Path, token);

            Assert.NotEqual(HttpStatusCode.OK, original.StatusCode);

            // ...while the cover an admin chose from it renders anyway.  this is
            // the whole point of the branch: a place tile has to draw for anyone
            // browsing, and the control sits in the choosing, not the serving.
            var cover = await friend.GetAsync(new Uri(updated!.CoverUrl!), token);

            Assert.Equal(HttpStatusCode.OK, cover.StatusCode);

            // and he is told about it in the listing, like everyone else
            var seen = await friend.GetFromJsonAsync<Place[]>(
                $"{ROUTE_LIST}?parent={city.ParentId}", JsonOptions, token);

            Assert.Equal(updated.CoverUrl, Assert.Single(seen!, p => p.Id == city.Id).CoverUrl);
        }
        finally
        {
            await writer.DeleteAsync(CoverRoute(city.Id), token);
        }
    }

    [Fact]
    public async Task ACoverIsPublishedFromTheQvgFillRenditionNotTheLargestOne()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await Usa(admin, "ny", "new-york");

        // MEDIA_TRAVEL_1 carries a full-hd and a qvg-fill.  the cover must come
        // from the qvg-fill, so every tile is the same shape whatever the source
        // photograph's aspect - picking the largest would undo that.
        var updated = await (await writer.PutAsJsonAsync(
            CoverRoute(city.Id), new PlaceCoverRequest(Constants.MEDIA_TRAVEL_1.Id), JsonOptions, token))
            .Content.ReadFromJsonAsync<Place>(JsonOptions, token);

        try
        {
            var published = await admin.GetAsync(new Uri(updated!.CoverUrl!), token);

            Assert.Equal(HttpStatusCode.OK, published.StatusCode);

            // each fixture rendition has a distinct body, so the bytes identify
            // exactly which file was copied
            Assert.Equal(
                ApiFactory.StubRendition(Constants.FILE_TRAVEL_1_COVER.Path),
                await published.Content.ReadAsByteArrayAsync(token));

            Assert.NotEqual(
                ApiFactory.StubRendition(Constants.FILE_TRAVEL_1.Path),
                await published.Content.ReadAsByteArrayAsync(token));
        }
        finally
        {
            await writer.DeleteAsync(CoverRoute(city.Id), token);
        }
    }

    [Fact]
    public async Task AMediaWithoutTheCoverRenditionIsRefused()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var uk = await Country(admin, "united-kingdom");

        // MEDIA_PLACE_UK exists, is visible to the admin and sits at this place -
        // it simply has no qvg-fill.  refusing is better than silently publishing
        // a differently shaped image.
        var response = await writer.PutAsJsonAsync(
            CoverRoute(uk.Id), new PlaceCoverRequest(Constants.MEDIA_PLACE_UK.Id), JsonOptions, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // and nothing was published on the way to failing
        var still = await admin.GetFromJsonAsync<Place>(PlaceRoute(uk.Id), JsonOptions, token);

        Assert.Null(still!.CoverUrl);
    }

    static string MergeRoute(Guid id) => $"{ROUTE_LIST}/{id}/merge";
    static string ParentRoute(Guid id) => $"{ROUTE_LIST}/{id}/parent";

    [Fact]
    public async Task ReshapingTheTreeRequiresTheScopeAndAdmin()
    {
        using var browse = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var noScope = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var notAdmin = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var (_, ny, _) = await Usa(browse, "ny", "new-york");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await noScope.PostAsJsonAsync(MergeRoute(ny.Id), new PlaceMergeRequest(ny.Id), JsonOptions, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await notAdmin.PutAsJsonAsync(ParentRoute(ny.Id), new PlaceParentRequest(null), JsonOptions, token)).StatusCode);
    }

    [Fact]
    public async Task MergeIsRefusedAcrossKindsAndIntoItself()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var (country, state, _) = await Usa(admin, "ny", "new-york");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await writer.PostAsJsonAsync(MergeRoute(country.Id), new PlaceMergeRequest(country.Id), JsonOptions, token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await writer.PostAsJsonAsync(MergeRoute(country.Id), new PlaceMergeRequest(state.Id), JsonOptions, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await writer.PostAsJsonAsync(MergeRoute(country.Id), new PlaceMergeRequest(Guid.CreateVersion7()), JsonOptions, token)).StatusCode);
    }

    [Fact]
    public async Task AReParentIsRefusedWhenTheParentDoesNotSitAbove()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var (country, state, city) = await Usa(admin, "ny", "new-york");

        // a state cannot sit under a city, and only a country may be a root
        Assert.Equal(HttpStatusCode.BadRequest,
            (await writer.PutAsJsonAsync(ParentRoute(state.Id), new PlaceParentRequest(city.Id), JsonOptions, token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await writer.PutAsJsonAsync(ParentRoute(city.Id), new PlaceParentRequest(null), JsonOptions, token)).StatusCode);

        // and the country is still where it was
        var unchanged = await admin.GetFromJsonAsync<Place>(PlaceRoute(state.Id), JsonOptions, token);

        Assert.Equal(country.Id, unchanged!.ParentId);
    }

    [Fact]
    public async Task SearchFindsAPlaceWithoutKnowingItsParent()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (_, _, boston) = await Usa(client, "ma", "boston");

        // no parent supplied, and the city is two levels down
        var hits = await client.GetFromJsonAsync<Place[]>(
            $"{ROUTE_LIST}?q=bost", JsonOptions, token);

        Assert.Equal(boston.Id, Assert.Single(hits!).Id);

        // kind still applies, so "every city called X" is one call
        var asState = await client.GetFromJsonAsync<Place[]>(
            $"{ROUTE_LIST}?q=bost&kind=state", JsonOptions, token);

        Assert.Empty(asState!);
    }

    [Fact]
    public async Task SearchIgnoresParentAndIsLiteralNotAPattern()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (country, _, boston) = await Usa(client, "ma", "boston");

        // a parent alongside a search is ignored rather than narrowing it - the
        // duplicates worth finding sit in different branches
        var scoped = await client.GetFromJsonAsync<Place[]>(
            $"{ROUTE_LIST}?q=bost&parent={country.Id}", JsonOptions, token);

        Assert.Equal(boston.Id, Assert.Single(scoped!).Id);

        // LIKE metacharacters are matched literally, so they find nothing rather
        // than everything
        foreach (var term in new[] { "%", "_" })
        {
            Assert.Empty((await client.GetFromJsonAsync<Place[]>(
                $"{ROUTE_LIST}?q={Uri.EscapeDataString(term)}", JsonOptions, token))!);
        }

        // and a blank term falls through to the country listing rather than
        // matching every place in the tree
        var blank = await client.GetFromJsonAsync<Place[]>(
            $"{ROUTE_LIST}?q={Uri.EscapeDataString("   ")}", JsonOptions, token);

        Assert.All(blank!, p => Assert.Equal("country", p.Kind));
    }

    [Fact]
    public async Task PlacesCarryTheirAncestorNames()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (country, state, city) = await Usa(client, "ny", "new-york");

        // this is what makes a search result legible without a second call: the
        // real library holds two cities called Zhuhai under two parents both
        // called Guangdong, and only the grandparent separates them
        Assert.Equal([country.Name, state.Name], city.AncestorNames);
        Assert.Equal([country.Name], state.AncestorNames);
        Assert.Empty(country.AncestorNames);
    }

    [Fact]
    public async Task APlaceReportsWhichPhotographItsCoverCameFrom()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await Usa(admin, "ny", "new-york");

        Assert.Null(city.CoverMediaId);

        var updated = await (await writer.PutAsJsonAsync(
            CoverRoute(city.Id), new PlaceCoverRequest(Constants.MEDIA_TRAVEL_1.Id), JsonOptions, token))
            .Content.ReadFromJsonAsync<Place>(JsonOptions, token);

        try
        {
            // the url names the published copy, so only this tells a picker which
            // original to show as selected
            Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, updated!.CoverMediaId);
        }
        finally
        {
            await writer.DeleteAsync(CoverRoute(city.Id), token);
        }

        var cleared = await admin.GetFromJsonAsync<Place>(PlaceRoute(city.Id), JsonOptions, token);

        Assert.Null(cleared!.CoverMediaId);
    }

    // the reverse lookup: which places one photograph could represent.  it hangs
    // off the media resource rather than this one, because it is asked with a
    // media id in hand
    static string MediaPlacesRoute(Guid id) => $"/api/v1/media/{id}/places";

    [Fact]
    public async Task GetMediaPlacesReturnsTheChainRootFirst()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        var (country, state, city) = await Usa(client, "ma", "boston");

        var chain = await client.GetFromJsonAsync<Place[]>(
            MediaPlacesRoute(Constants.MEDIA_PLACE_MA.Id), JsonOptions, token);

        // country first, so a client can label the rungs in the order it was
        // handed them - and whole places, not the breadcrumb, so each one carries
        // the cover a caller would be replacing
        Assert.Equal([country.Id, state.Id, city.Id], chain!.Select(p => p.Id));
        Assert.Equal(["country", "state", "city"], chain!.Select(p => p.Kind));
        Assert.Equal(
            [country.MediaCount, state.MediaCount, city.MediaCount],
            chain!.Select(p => p.MediaCount));
    }

    [Fact]
    public async Task MediaPlacesFollowTheLocationOverride()
    {
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        // MEDIA_PLACE_OVERRIDE is recorded at LOCATION_NY and corrected to
        // LOCATION_MA.  the override is the majority path in production, so this
        // must name Boston - offering New York would let an admin publish a cover
        // for a place the photograph was never taken at
        var (_, _, boston) = await Usa(client, "ma", "boston");

        var chain = await client.GetFromJsonAsync<Place[]>(
            MediaPlacesRoute(Constants.MEDIA_PLACE_OVERRIDE.Id), JsonOptions, token);

        Assert.Equal(boston.Id, chain!.Last().Id);
        Assert.DoesNotContain(chain!, p => p.Slug == "new-york");
    }

    [Fact]
    public async Task MediaPlacesCarryTheCoverEachRungCurrentlyHas()
    {
        using var admin = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.MediaRead);
        using var writer = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);
        var token = TestContext.Current.CancellationToken;

        var (_, _, city) = await Usa(admin, "ny", "new-york");

        await writer.PutAsJsonAsync(
            CoverRoute(city.Id), new PlaceCoverRequest(Constants.MEDIA_TRAVEL_1.Id), JsonOptions, token);

        try
        {
            var chain = await admin.GetFromJsonAsync<Place[]>(
                MediaPlacesRoute(Constants.MEDIA_NATURE_1.Id), JsonOptions, token);

            var newYork = Assert.Single(chain!, p => p.Id == city.Id);

            // this is the whole reason whole places are returned: a screen offering
            // to replace a cover has to show the one in force, and which photograph
            // it came from
            Assert.NotNull(newYork.CoverUrl);
            Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, newYork.CoverMediaId);

            // the levels above it are untouched by a city's choice
            Assert.All(chain!.Where(p => p.Id != city.Id), p => Assert.Null(p.CoverUrl));
        }
        finally
        {
            await writer.DeleteAsync(CoverRoute(city.Id), token);
        }
    }

    [Fact]
    public async Task MediaPlacesAreEmptyForMediaTheCallerCannotSee()
    {
        using var friend = Client(Constants.EXTERNAL_ID_JOHNDOE, ApiScopes.MediaRead);
        var token = TestContext.Current.CancellationToken;

        // MEDIA_PLACE_UK is reachable only through CATEGORY_FOOD, which
        // ROLE_FRIEND does not hold.  an empty chain rather than a 404, so this
        // cannot be used to confirm the media exists - and it is the same answer
        // a media with no location gives, which is the point
        var chain = await friend.GetFromJsonAsync<Place[]>(
            MediaPlacesRoute(Constants.MEDIA_PLACE_UK.Id), JsonOptions, token);

        Assert.Empty(chain!);

        var unknown = await friend.GetFromJsonAsync<Place[]>(
            MediaPlacesRoute(Guid.CreateVersion7()), JsonOptions, token);

        Assert.Empty(unknown!);
    }

    [Fact]
    public async Task GetMediaPlacesRequiresTheMediaReadScope()
    {
        // naming where a photograph was taken is browsing; the location scopes are
        // for maintaining the geocode and administering the tree
        using var client = Client(Constants.EXTERNAL_ID_USERADMIN, ApiScopes.LocationWrite);

        var response = await client.GetAsync(
            MediaPlacesRoute(Constants.MEDIA_PLACE_MA.Id), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
