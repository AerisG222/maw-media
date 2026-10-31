using Dapper;
using MawMedia.Models;
using MawMedia.Services;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace MawMedia.Services.Tests;

// narrowing a listing to one kind of media, and search to favourites.
//
// every seeded media is a photo, so this builds its own: a private user who can
// see two categories, neither of which anybody else can.  no count asserted
// elsewhere can move while these run in parallel with the rest of the suite.
//
//   Mixed      a photo, a favourite photo and a video.  a favourite category.
//   PhotosOnly a photo, and a video restricted to a role the user lacks - so it
//              exists, but must not make this category answer to "videos".
//
// the person appears in, and the place holds, all of the media.
public class MediaTypeFilterTests
{
    const string BASE_URL = "https://example.com";

    readonly TestFixture _fixture;

    public MediaTypeFilterTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task PersonMediaNarrowsToOneType()
    {
        var u = await Create();
        var repo = FaceRepo();
        var token = TestContext.Current.CancellationToken;

        async Task<IEnumerable<Guid>> Ids(string? type, bool favorites = false) =>
            (await repo.GetPersonMedia(u.User, BASE_URL, u.Person, 0, 24, favorites, mediaType: type, token: token))
                .Results.Select(m => m.Id);

        Assert.Equivalent(new[] { u.Photo, u.FavoritePhoto, u.Video, u.OtherPhoto }, await Ids(null));
        Assert.Equivalent(new[] { u.Photo, u.FavoritePhoto, u.OtherPhoto }, await Ids("photo"));
        Assert.Equivalent(new[] { u.Video }, await Ids("video"));

        // both filters at once mean both
        Assert.Equivalent(new[] { u.FavoritePhoto }, await Ids("photo", true));
        Assert.Empty(await Ids("video", true));
    }

    // filtered before paging, or a page of photos would hide the only video on page two
    [Fact]
    public async Task PersonMediaFiltersBeforePaging()
    {
        var u = await Create();

        var result = await FaceRepo().GetPersonMedia(
            u.User, BASE_URL, u.Person, 0, 1, mediaType: "video", token: TestContext.Current.CancellationToken);

        Assert.Equal(u.Video, Assert.Single(result.Results).Id);
        Assert.False(result.HasMoreResults);
    }

    [Fact]
    public async Task PersonCategoriesNarrowToThoseHoldingTheType()
    {
        var u = await Create();
        var repo = FaceRepo();
        var token = TestContext.Current.CancellationToken;

        async Task<IEnumerable<Guid>> Ids(string? type) =>
            (await repo.GetPersonCategories(u.User, BASE_URL, u.Person, 0, 24, mediaType: type, token: token))
                .Results.Select(c => c.Id);

        Assert.Equivalent(new[] { u.Mixed, u.PhotosOnly }, await Ids(null));
        Assert.Equivalent(new[] { u.Mixed, u.PhotosOnly }, await Ids("photo"));
        Assert.Equivalent(new[] { u.Mixed }, await Ids("video"));
    }

    [Fact]
    public async Task PlaceMediaNarrowsToOneType()
    {
        var u = await Create();
        var repo = PlaceRepo();
        var token = TestContext.Current.CancellationToken;

        async Task<IEnumerable<Guid>> Ids(string? type) =>
            (await repo.GetPlaceMedia(u.User, BASE_URL, u.Place, 0, 24, mediaType: type, token: token))
                .Results.Select(m => m.Id);

        Assert.Equivalent(new[] { u.Photo, u.FavoritePhoto, u.Video, u.OtherPhoto }, await Ids(null));
        Assert.Equivalent(new[] { u.Video }, await Ids("video"));
        Assert.Equivalent(new[] { u.Photo, u.FavoritePhoto, u.OtherPhoto }, await Ids("photo"));
    }

    [Fact]
    public async Task PlaceCategoriesNarrowToThoseHoldingTheType()
    {
        var u = await Create();
        var repo = PlaceRepo();
        var token = TestContext.Current.CancellationToken;

        async Task<IEnumerable<Guid>> Ids(string? type) =>
            (await repo.GetPlaceCategories(u.User, BASE_URL, u.Place, 0, 24, mediaType: type, token: token))
                .Results.Select(c => c.Id);

        Assert.Equivalent(new[] { u.Mixed, u.PhotosOnly }, await Ids(null));
        Assert.Equivalent(new[] { u.Mixed }, await Ids("video"));
    }

    [Fact]
    public async Task SearchNarrowsToFavoritesAndToOneType()
    {
        var u = await Create();
        var repo = CategoryRepo();
        var token = TestContext.Current.CancellationToken;

        async Task<IEnumerable<Guid>> Ids(bool favorites = false, string? type = null) =>
            (await repo.Search(u.User, BASE_URL, u.Tag, 0, 24, favorites, type, token))
                .Results.Select(c => c.Id);

        Assert.Equivalent(new[] { u.Mixed, u.PhotosOnly }, await Ids());

        Assert.Equivalent(new[] { u.Mixed }, await Ids(favorites: true));

        Assert.Equivalent(new[] { u.Mixed, u.PhotosOnly }, await Ids(type: "photo"));

        // PhotosOnly holds a video, but one this user is not allowed to know about
        Assert.Equivalent(new[] { u.Mixed }, await Ids(type: "video"));

        Assert.Equivalent(new[] { u.Mixed }, await Ids(true, "video"));
    }

    // a favourite photo inside a category is not a favourite category
    [Fact]
    public async Task SearchFavoritesMeansTheCategoryItself()
    {
        var u = await Create(favoriteCategory: false);

        var result = await CategoryRepo().Search(
            u.User, BASE_URL, u.Tag, 0, 24, favoritesOnly: true, token: TestContext.Current.CancellationToken);

        Assert.Empty(result.Results);
    }

    async Task<FilterUniverse> Create(bool favoriteCategory = true)
    {
        // the random tail of a v7 guid, for the reason PlaceAdminTests gives
        var tag = "filter" + Guid.CreateVersion7().ToString("N")[^8..];

        var u = new FilterUniverse(
            tag,
            User: Guid.CreateVersion7(),
            Person: Guid.CreateVersion7(),
            Mixed: Guid.CreateVersion7(),
            PhotosOnly: Guid.CreateVersion7(),
            Photo: Guid.CreateVersion7(),
            FavoritePhoto: Guid.CreateVersion7(),
            Video: Guid.CreateVersion7(),
            OtherPhoto: Guid.CreateVersion7(),
            HiddenVideo: Guid.CreateVersion7(),
            Place: Guid.Empty);

        await using var conn = _fixture.SetupDataSource.CreateConnection();

        await conn.ExecuteAsync(
            """
            INSERT INTO media.user (id, created, modified, name, email)
            VALUES (@user, NOW(), NOW(), @tag, @tag || '@example.test');

            INSERT INTO media.role (id, name, created, created_by) VALUES
                (@role, @tag || '-reader', NOW(), @createdBy),
                (@otherRole, @tag || '-other', NOW(), @createdBy);

            INSERT INTO media.user_role (user_id, role_id, created, created_by)
            VALUES (@user, @role, NOW(), @createdBy);

            INSERT INTO media.category (id, name, slug, effective_date, created, created_by, modified, modified_by) VALUES
                (@mixed,      @tag || ' mixed',  @tag || '-mixed',  '2001-01-02', NOW(), @createdBy, NOW(), @createdBy),
                (@photosOnly, @tag || ' photos', @tag || '-photos', '2001-01-01', NOW(), @createdBy, NOW(), @createdBy);

            INSERT INTO media.category_role (category_id, role_id, created, created_by) VALUES
                (@mixed,      @role, NOW(), @createdBy),
                (@photosOnly, @role, NOW(), @createdBy);

            INSERT INTO media.media (id, type_id, location_id, created, created_by, modified, modified_by) VALUES
                (@photo,         @typePhoto, @location, NOW(), @createdBy, NOW(), @createdBy),
                (@favoritePhoto, @typePhoto, @location, NOW(), @createdBy, NOW(), @createdBy),
                (@video,         @typeVideo, @location, NOW(), @createdBy, NOW(), @createdBy),
                (@otherPhoto,    @typePhoto, @location, NOW(), @createdBy, NOW(), @createdBy),
                (@hiddenVideo,   @typeVideo, @location, NOW(), @createdBy, NOW(), @createdBy);

            INSERT INTO media.category_media (category_id, media_id, slug, is_teaser, created, created_by, modified, modified_by) VALUES
                (@mixed,      @photo,         'photo',    true,  NOW(), @createdBy, NOW(), @createdBy),
                (@mixed,      @favoritePhoto, 'favorite', false, NOW(), @createdBy, NOW(), @createdBy),
                (@mixed,      @video,         'video',    false, NOW(), @createdBy, NOW(), @createdBy),
                (@photosOnly, @otherPhoto,    'other',    true,  NOW(), @createdBy, NOW(), @createdBy),
                (@photosOnly, @hiddenVideo,   'hidden',   false, NOW(), @createdBy, NOW(), @createdBy);

            INSERT INTO media.file (id, media_id, type_id, scale_id, width, height, bytes, path) VALUES
                (gen_random_uuid(), @photo,         @typePhoto, @scale, 320, 240, 1, '/assets/' || @tag || '/photo.avif'),
                (gen_random_uuid(), @favoritePhoto, @typePhoto, @scale, 320, 240, 1, '/assets/' || @tag || '/favorite.avif'),
                (gen_random_uuid(), @video,         @typeVideo, @scale, 320, 240, 1, '/assets/' || @tag || '/video.avif'),
                (gen_random_uuid(), @otherPhoto,    @typePhoto, @scale, 320, 240, 1, '/assets/' || @tag || '/other.avif'),
                (gen_random_uuid(), @hiddenVideo,   @typeVideo, @scale, 320, 240, 1, '/assets/' || @tag || '/hidden.avif');

            INSERT INTO media.media_role (media_id, role_id, created, created_by)
            VALUES (@hiddenVideo, @otherRole, NOW(), @createdBy);

            INSERT INTO media.favorite (media_id, created_by, created)
            VALUES (@favoritePhoto, @user, NOW());

            INSERT INTO media.person (id, name, slug, face_count, source_revision, published)
            VALUES (@person, @tag || ' person', @tag || '-person', 4, 1, NOW());

            INSERT INTO media.face (id, media_id, person_id, box_x, box_y, box_width, box_height, detection_score, source_revision, published) VALUES
                (gen_random_uuid(), @photo,         @person, 0.1, 0.1, 0.2, 0.2, 0.99, 1, NOW()),
                (gen_random_uuid(), @favoritePhoto, @person, 0.1, 0.1, 0.2, 0.2, 0.99, 1, NOW()),
                (gen_random_uuid(), @video,         @person, 0.1, 0.1, 0.2, 0.2, 0.99, 1, NOW()),
                (gen_random_uuid(), @otherPhoto,    @person, 0.1, 0.1, 0.2, 0.2, 0.99, 1, NOW()),
                (gen_random_uuid(), @hiddenVideo,   @person, 0.1, 0.1, 0.2, 0.2, 0.99, 1, NOW());
            """,
            new
            {
                tag,
                user = u.User,
                person = u.Person,
                mixed = u.Mixed,
                photosOnly = u.PhotosOnly,
                photo = u.Photo,
                favoritePhoto = u.FavoritePhoto,
                video = u.Video,
                otherPhoto = u.OtherPhoto,
                hiddenVideo = u.HiddenVideo,
                role = Guid.CreateVersion7(),
                otherRole = Guid.CreateVersion7(),
                createdBy = Constants.USER_ADMIN,
                location = Constants.LOCATION_MA.Id,
                typePhoto = Constants.TYPE_PHOTO,
                typeVideo = Constants.TYPE_VIDEO,
                scale = Constants.SCALE_QVG_FILL
            });

        if (favoriteCategory)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO media.category_favorite (created_by, category_id, created)
                VALUES (@user, @mixed, NOW());
                """,
                new { user = u.User, mixed = u.Mixed });
        }

        // search reads this view, which nothing refreshes on its own.  only this
        // universe's user can see these categories, so no other test's answer moves.
        await conn.ExecuteAsync("REFRESH MATERIALIZED VIEW media.category_search;");

        var place = await conn.QuerySingleAsync<Guid>(
            "SELECT place_id FROM media.user_location WHERE user_id = @user AND media_id = @photo LIMIT 1;",
            new { user = u.User, photo = u.Photo });

        return u with { Place = place };
    }

    FaceRepository FaceRepo() =>
        new(
            new FakeLogger<FaceRepository>(),
            _fixture.DataSource.CreateConnection(),
            new AssetPathBuilder(),
            new FakeHybridCache()
        );

    CategoryRepository CategoryRepo() =>
        new(
            new FakeLogger<CategoryRepository>(),
            _fixture.DataSource.CreateConnection(),
            new FakeHybridCache(),
            new AssetPathBuilder()
        );

    PlaceRepository PlaceRepo() =>
        new(
            new FakeLogger<PlaceRepository>(),
            _fixture.DataSource.CreateConnection(),
            new AssetPathBuilder(),
            new FakeHybridCache(),
            // these reads never publish a cover, so nothing is written to either
            new PlaceCoverStore(
                new FakeLogger<PlaceCoverStore>(),
                Options.Create(new PlaceCoverConfig { RootDirectory = TestDir("covers") }),
                Options.Create(new AssetConfig { RootDirectory = TestDir("assets") })
            )
        );

    static string TestDir(string name) =>
        Path.Combine(Path.GetTempPath(), "maw-media-type-filter", name);

    record FilterUniverse(
        string Tag,
        Guid User,
        Guid Person,
        Guid Mixed,
        Guid PhotosOnly,
        Guid Photo,
        Guid FavoritePhoto,
        Guid Video,
        Guid OtherPhoto,
        Guid HiddenVideo,
        Guid Place
    );
}
