using Dapper;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace MawMedia.Services.Tests;

// a media restricted to fewer roles than its category grants - media.media_role,
// applied in media.user_media.
//
// the rule is applied in one place, but it has to hold on every path that hands
// out media or their files, and each of those reaches media.user_media a
// different way.  so these tests ask the same question - may this caller have
// this photo - through each of them: the media itself, its category page, the
// asset check that guards the file, random media, faces and places.
//
// every subject here lives in a private universe built per test: its own roles,
// its own users, its own category.  no seeded user holds these roles, so none of
// this is visible to the rest of the suite, and no count asserted anywhere else
// can move while these run in parallel with it.
public class MediaRestrictionTests
{
    const string BASE_URL = "https://example.test";

    readonly TestFixture _fixture;

    public MediaRestrictionTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task ARestrictedMediaIsWithheldFromARoleItWasNotRestrictedTo()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        Assert.Null(await Media().GetMedia(u.Friend, BASE_URL, u.AdminOnly.Id, token));
        Assert.Null(await Media().GetMediaFile(u.Friend, u.AdminOnly.Path, token));
        Assert.False(await Media().AllowAccessToAsset(u.Friend, u.AdminOnly.Path, token));
        Assert.DoesNotContain(u.AdminOnly.Id, await CategoryPage(u.Friend, u.Category));
        Assert.False(await Faces().CanViewFace(u.Friend, u.AdminOnly.FaceId, token));
        Assert.Equal(0, await PlacesHolding(u.Friend, u.AdminOnly.Id));
    }

    [Fact]
    public async Task ARestrictedMediaIsShownToTheRoleItWasRestrictedTo()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        Assert.NotNull(await Media().GetMedia(u.Admin, BASE_URL, u.AdminOnly.Id, token));
        Assert.NotNull(await Media().GetMediaFile(u.Admin, u.AdminOnly.Path, token));
        Assert.True(await Media().AllowAccessToAsset(u.Admin, u.AdminOnly.Path, token));
        Assert.Contains(u.AdminOnly.Id, await CategoryPage(u.Admin, u.Category));
        Assert.True(await Faces().CanViewFace(u.Admin, u.AdminOnly.FaceId, token));
        Assert.Equal(1, await PlacesHolding(u.Admin, u.AdminOnly.Id));
    }

    [Fact]
    public async Task HoldingTheRoleAlongsideAnotherStillGrantsIt()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        // the case a per user deny list gets wrong: this caller is also a friend,
        // and must not lose the photo for it.  the rule is decided per role.
        Assert.NotNull(await Media().GetMedia(u.Both, BASE_URL, u.AdminOnly.Id, token));
        Assert.True(await Media().AllowAccessToAsset(u.Both, u.AdminOnly.Path, token));
        Assert.Contains(u.AdminOnly.Id, await CategoryPage(u.Both, u.Category));
    }

    [Fact]
    public async Task ARestrictionCanOnlyNarrowTheCategory()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        // restricted to a role the category is never granted.  that grants
        // nothing - not to the outsider, whose role cannot reach into a category
        // it was never given, and not to the category's own roles either, which
        // are not on the list
        foreach (var caller in new[] { u.Admin, u.Friend, u.Both, u.Outsider })
        {
            Assert.Null(await Media().GetMedia(caller, BASE_URL, u.OutsiderOnly.Id, token));
            Assert.False(await Media().AllowAccessToAsset(caller, u.OutsiderOnly.Path, token));
        }
    }

    [Fact]
    public async Task AnUnrestrictedMediaStillFollowsItsCategory()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        foreach (var caller in new[] { u.Admin, u.Friend, u.Both })
        {
            Assert.NotNull(await Media().GetMedia(caller, BASE_URL, u.Open.Id, token));
            Assert.True(await Media().AllowAccessToAsset(caller, u.Open.Path, token));
        }

        Assert.Null(await Media().GetMedia(u.Outsider, BASE_URL, u.Open.Id, token));
    }

    [Fact]
    public async Task RandomMediaNeverDrawsAWithheldMedia()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        // the friend can see exactly three media in the whole library - the
        // unrestricted ones - so every draw must be among them.  this also runs the
        // exact fallback path, since a sample of the library will rarely contain
        // this tiny category
        Guid[] visible = [u.Open.Id, u.Spare.Id, u.Covered.Id];

        for (var i = 0; i < 10; i++)
        {
            var drawn = await Media().GetRandomMedia(u.Friend, BASE_URL, 10, token);

            Assert.NotEmpty(drawn);
            Assert.All(drawn, m => Assert.Contains(m.Id, visible));
        }
    }

    // ---- the places a restricted media must not reach by another route --------
    //
    // a teaser, a place cover and a person's preferred face are each shown to an
    // audience wider than the media's own: everyone who can see the category, the
    // place, or the person.  so each is either refused or withheld.

    [Fact]
    public async Task APreferredFaceOnAWithheldMediaIsNotListed()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);
        var friendCache = new FakeHybridCache();

        // the friend sees the person - through the face on the open photo - but
        // not the preferred face, which sits on the admin only photo
        var person = (await Faces(friendCache).GetPersons(u.Friend, BASE_URL, token: token))
            .Single(p => p.Id == u.Person);

        Assert.Null(person.PreferredFaceId);

        // and it is not quietly granted either.  the listing primes the face
        // access cache with every preferred face it returns, which is exactly how
        // returning this one would have handed the friend the crop
        Assert.DoesNotContain(CacheKeyBuilder.CanViewFace(u.Friend, u.AdminOnly.FaceId), friendCache.SetKeys);

        var forAdmin = (await Faces().GetPersons(u.Admin, BASE_URL, token: token))
            .Single(p => p.Id == u.Person);

        Assert.Equal(u.AdminOnly.FaceId, forAdmin.PreferredFaceId);
    }

    [Fact]
    public async Task ARestrictedMediaCannotBecomeTheTeaser()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        // the admin owns the category and can see the photo - the refusal is about
        // the restriction alone
        var outcome = await Categories().SetTeaserMedia(u.Admin, u.Category, u.AdminOnly.Id, token);

        Assert.Equal(CategoryTeaserOutcome.MediaRestricted, outcome);
        Assert.Equal(u.Open.Id, await Scalar<Guid>(
            "SELECT media_id FROM media.category_media WHERE category_id = @id AND is_teaser", u.Category));
    }

    [Fact]
    public async Task ARestrictedMediaCannotBecomeAPlaceCover()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);
        var place = await PlaceOf(Constants.LOCATION_MA.Id);

        // refused before the rendition is published.  that is checked by more than
        // the outcome: this media has no file on disk, so had the repository gone
        // on to publish it the copy would have thrown
        var outcome = await Places().SetPlaceCover(u.Admin, place, u.AdminOnly.Id, token);

        Assert.Equal(PlaceCoverOutcome.MediaRestricted, outcome);
        Assert.NotEqual(u.AdminOnly.Id, await Scalar<Guid?>(
            "SELECT cover_media_id FROM media.place WHERE id = @id", place));
    }

    [Fact]
    public async Task TheDatabaseRefusesARestrictedCoverOnItsOwn()
    {
        var u = await RestrictionUniverse.Create(_fixture);
        var place = await PlaceOf(Constants.LOCATION_MA.Id);

        await using var conn = _fixture.DataSource.CreateConnection();

        // for any caller that skips the repository's check: an admin, at a place
        // the media is filed under, who can see it - refused only for the
        // restriction
        var result = await conn.ExecuteScalarAsync<int>(
            """
            SELECT result FROM media.set_place_cover(
                @admin, @place, @media,
                (SELECT id FROM media.file WHERE media_id = @media LIMIT 1))
            """,
            new { admin = u.Admin, place, media = u.AdminOnly.Id });

        Assert.Equal(4, result);
    }

    [Fact]
    public async Task MediaTypesOnlyNameMediaTheCallerCanSee()
    {
        var token = TestContext.Current.CancellationToken;
        var u = await RestrictionUniverse.Create(_fixture);

        // the only video in the category is restricted to a role nobody here
        // holds.  naming "video" on the tile would say it exists
        foreach (var caller in new[] { u.Admin, u.Friend })
        {
            var category = await Categories().GetCategory(caller, u.Category, BASE_URL, token);

            Assert.NotNull(category);
            Assert.Equal(["photo"], category.MediaTypes);
        }
    }

    async Task<Guid> PlaceOf(Guid locationId) =>
        await Scalar<Guid>("SELECT place_id FROM media.location WHERE id = @id", locationId);

    async Task<T?> Scalar<T>(string sql, Guid id)
    {
        await using var conn = _fixture.DataSource.CreateConnection();

        return await conn.ExecuteScalarAsync<T>(sql, new { id });
    }

    async Task<IEnumerable<Guid>> CategoryPage(Guid userId, Guid categoryId) =>
        (await Categories().GetCategoryMedia(userId, BASE_URL, categoryId, TestContext.Current.CancellationToken))
            .Select(m => m.Id);

    async Task<int> PlacesHolding(Guid userId, Guid mediaId)
    {
        await using var conn = _fixture.DataSource.CreateConnection();

        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM media.user_location WHERE user_id = @userId AND media_id = @mediaId",
            new { userId, mediaId });
    }

    MediaRepository Media() =>
        new(new FakeLogger<MediaRepository>(), _fixture.DataSource.CreateConnection(), new FakeHybridCache(), new AssetPathBuilder());

    CategoryRepository Categories() =>
        new(new FakeLogger<CategoryRepository>(), _fixture.DataSource.CreateConnection(), new FakeHybridCache(), new AssetPathBuilder());

    FaceRepository Faces(FakeHybridCache? cache = null) =>
        new(new FakeLogger<FaceRepository>(), _fixture.DataSource.CreateConnection(), new AssetPathBuilder(), cache ?? new FakeHybridCache());

    PlaceRepository Places() =>
        new(
            new FakeLogger<PlaceRepository>(),
            _fixture.DataSource.CreateConnection(),
            new AssetPathBuilder(),
            new FakeHybridCache(),
            new PlaceCoverStore(
                new FakeLogger<PlaceCoverStore>(),
                Options.Create(new PlaceCoverConfig { RootDirectory = TestDir("restriction-covers") }),
                Options.Create(new AssetConfig { RootDirectory = TestDir("restriction-assets") })));

    static string TestDir(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "maw-media-restriction", name);

        Directory.CreateDirectory(path);

        return path;
    }
}
