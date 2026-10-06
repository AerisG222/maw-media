using Dapper;
using Microsoft.Extensions.Logging.Testing;

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
        var u = await CreateUniverse();

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
        var u = await CreateUniverse();

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
        var u = await CreateUniverse();

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
        var u = await CreateUniverse();

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
        var u = await CreateUniverse();

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
        var u = await CreateUniverse();

        // the friend can see exactly one media in the whole library - the open
        // one - so every draw must be it.  this also runs the exact fallback path,
        // since a sample of the library will rarely contain this tiny category
        for (var i = 0; i < 10; i++)
        {
            var drawn = await Media().GetRandomMedia(u.Friend, BASE_URL, 10, token);

            Assert.All(drawn, m => Assert.Equal(u.Open.Id, m.Id));
        }
    }

    // two roles the category grants, one it does not, and a user for each
    // combination that matters.  three media: one unrestricted, one restricted to
    // the admin role, one restricted to the role the category never grants.  each
    // has a file, and the admin only one also has a face and a place.
    async Task<Universe> CreateUniverse()
    {
        // the random tail of a v7 guid, for the reason PlaceAdminTests gives
        var tag = Guid.CreateVersion7().ToString("N")[^8..];

        var u = new Universe(
            Category: Guid.CreateVersion7(),
            Admin: Guid.CreateVersion7(),
            Friend: Guid.CreateVersion7(),
            Both: Guid.CreateVersion7(),
            Outsider: Guid.CreateVersion7(),
            Open: new Subject(Guid.CreateVersion7(), $"/assets/2001/restrict-{tag}/qvg-fill/open.avif", Guid.CreateVersion7()),
            AdminOnly: new Subject(Guid.CreateVersion7(), $"/assets/2001/restrict-{tag}/qvg-fill/admin.avif", Guid.CreateVersion7()),
            OutsiderOnly: new Subject(Guid.CreateVersion7(), $"/assets/2001/restrict-{tag}/qvg-fill/outsider.avif", Guid.CreateVersion7()));

        var roleAdmin = Guid.CreateVersion7();
        var roleFriend = Guid.CreateVersion7();
        var roleOutsider = Guid.CreateVersion7();

        await using var conn = _fixture.SetupDataSource.CreateConnection();

        await conn.ExecuteAsync(
            """
            INSERT INTO media.user (id, created, modified, name, email) VALUES
                (@admin,    NOW(), NOW(), @tag || ' admin',    @tag || '-admin@example.test'),
                (@friend,   NOW(), NOW(), @tag || ' friend',   @tag || '-friend@example.test'),
                (@both,     NOW(), NOW(), @tag || ' both',     @tag || '-both@example.test'),
                (@outsider, NOW(), NOW(), @tag || ' outsider', @tag || '-outsider@example.test');

            INSERT INTO media.role (id, name, created, created_by) VALUES
                (@roleAdmin,    @tag || '-admin',    NOW(), @createdBy),
                (@roleFriend,   @tag || '-friend',   NOW(), @createdBy),
                (@roleOutsider, @tag || '-outsider', NOW(), @createdBy);

            INSERT INTO media.user_role (user_id, role_id, created, created_by) VALUES
                (@admin,    @roleAdmin,    NOW(), @createdBy),
                (@friend,   @roleFriend,   NOW(), @createdBy),
                (@both,     @roleAdmin,    NOW(), @createdBy),
                (@both,     @roleFriend,   NOW(), @createdBy),
                (@outsider, @roleOutsider, NOW(), @createdBy);

            INSERT INTO media.category (id, name, slug, effective_date, created, created_by, modified, modified_by)
            VALUES (@category, @tag || ' restricted', 'restrict-' || @tag, '2001-01-01', NOW(), @createdBy, NOW(), @createdBy);

            -- the category is granted to admin and friend; the outsider role is
            -- deliberately left off
            INSERT INTO media.category_role (category_id, role_id, created, created_by) VALUES
                (@category, @roleAdmin,  NOW(), @createdBy),
                (@category, @roleFriend, NOW(), @createdBy);

            INSERT INTO media.media (id, type_id, location_id, created, created_by, modified, modified_by) VALUES
                (@open,         @photo, NULL,        NOW(), @createdBy, NOW(), @createdBy),
                (@adminOnly,    @photo, @locationMa, NOW(), @createdBy, NOW(), @createdBy),
                (@outsiderOnly, @photo, NULL,        NOW(), @createdBy, NOW(), @createdBy);

            INSERT INTO media.category_media (category_id, media_id, slug, is_teaser, created, created_by, modified, modified_by) VALUES
                (@category, @open,         'open',     true,  NOW(), @createdBy, NOW(), @createdBy),
                (@category, @adminOnly,    'admin',    false, NOW(), @createdBy, NOW(), @createdBy),
                (@category, @outsiderOnly, 'outsider', false, NOW(), @createdBy, NOW(), @createdBy);

            INSERT INTO media.file (id, media_id, type_id, scale_id, width, height, bytes, path) VALUES
                (gen_random_uuid(), @open,         @photo, @scale, 320, 240, 1, @openPath),
                (gen_random_uuid(), @adminOnly,    @photo, @scale, 320, 240, 1, @adminPath),
                (gen_random_uuid(), @outsiderOnly, @photo, @scale, 320, 240, 1, @outsiderPath);

            INSERT INTO media.face (id, media_id, person_id, box_x, box_y, box_width, box_height, detection_score, source_revision, published)
            VALUES (@adminFace, @adminOnly, NULL, 0.1, 0.1, 0.2, 0.2, 0.99, 1, NOW());

            INSERT INTO media.media_role (media_id, role_id, created, created_by) VALUES
                (@adminOnly,    @roleAdmin,    NOW(), @createdBy),
                (@outsiderOnly, @roleOutsider, NOW(), @createdBy);
            """,
            new
            {
                tag,
                createdBy = Constants.USER_ADMIN,
                admin = u.Admin,
                friend = u.Friend,
                both = u.Both,
                outsider = u.Outsider,
                roleAdmin,
                roleFriend,
                roleOutsider,
                category = u.Category,
                photo = Constants.TYPE_PHOTO,
                scale = Constants.SCALE_QVG_FILL,
                locationMa = Constants.LOCATION_MA.Id,
                open = u.Open.Id,
                adminOnly = u.AdminOnly.Id,
                outsiderOnly = u.OutsiderOnly.Id,
                openPath = u.Open.Path,
                adminPath = u.AdminOnly.Path,
                outsiderPath = u.OutsiderOnly.Path,
                adminFace = u.AdminOnly.FaceId
            });

        return u;
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

    FaceRepository Faces() =>
        new(new FakeLogger<FaceRepository>(), _fixture.DataSource.CreateConnection(), new AssetPathBuilder(), new FakeHybridCache());

    record Subject(Guid Id, string Path, Guid FaceId);

    record Universe(
        Guid Category,
        Guid Admin,
        Guid Friend,
        Guid Both,
        Guid Outsider,
        Subject Open,
        Subject AdminOnly,
        Subject OutsiderOnly);
}
