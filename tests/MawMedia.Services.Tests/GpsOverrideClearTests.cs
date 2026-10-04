using Dapper;
using Microsoft.Extensions.Logging.Testing;

namespace MawMedia.Services.Tests;

// clearing an override is destructive in the way PlaceAdminTests means it: the
// seeded MEDIA_PLACE_OVERRIDE is filed under Boston *only* through its override,
// and half the place suite reads it.  so every subject here is built by the test
// itself and never drawn from the seed - test classes run in parallel against one
// database, and a shared subject would be a flake waiting to happen.
public class GpsOverrideClearTests
{
    readonly TestFixture _fixture;

    public GpsOverrideClearTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task OwnerClearsTheOverrideAndTheRecordedLocationSurvives()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await CreateMedia(Constants.USER_ADMIN, 1);
        var media = scratch.Media[0];

        Assert.True(await GetRepo().ClearGpsOverride(Constants.USER_ADMIN, media.Id, token));

        var row = await MediaRow(media.Id);

        Assert.Null(row.LocationOverrideId);

        // the fallback is the point: the media now reads as taken where its own
        // file said, so the recorded coordinate must be untouched
        Assert.Equal(media.RecordedLocationId, row.LocationId);
    }

    [Fact]
    public async Task ClearingLeavesTheOverrideLocationInPlace()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await CreateMedia(Constants.USER_ADMIN, 1);
        var media = scratch.Media[0];

        Assert.True(await GetRepo().ClearGpsOverride(Constants.USER_ADMIN, media.Id, token));

        // deliberately kept even though nothing points at it now - a location row
        // can carry a reverse geocode that was paid for, and deleting it would mean
        // paying again the next time anyone chose this coordinate
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) FROM media.location WHERE id = @id", media.OverrideLocationId));
    }

    [Fact]
    public async Task ANonOwnerCannotClear()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await CreateMedia(Constants.USER_ADMIN, 1);
        var media = scratch.Media[0];

        // johndoe can see media but owns no category holding this one, which is
        // the rule set_media_gps_override applies too
        Assert.False(await GetRepo().ClearGpsOverride(Constants.USER_JOHNDOE, media.Id, token));

        Assert.Equal(media.OverrideLocationId, (await MediaRow(media.Id)).LocationOverrideId);
    }

    [Fact]
    public async Task ClearingAnAbsentOverrideSucceeds()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await CreateMedia(Constants.USER_ADMIN, 1);
        var repo = GetRepo();

        Assert.True(await repo.ClearGpsOverride(Constants.USER_ADMIN, scratch.Media[0].Id, token));

        // the caller's intent - no override - is already true, so a repeat is a
        // success rather than an error a client would read as a permission failure
        Assert.True(await repo.ClearGpsOverride(Constants.USER_ADMIN, scratch.Media[0].Id, token));
    }

    [Fact]
    public async Task ClearingAnUnknownMediaFails()
    {
        var token = TestContext.Current.CancellationToken;

        // the same answer set_media_gps_override gives, so the pair cannot be used
        // to tell an unknown id from one the caller may not change
        Assert.False(await GetRepo().ClearGpsOverride(Constants.USER_ADMIN, Guid.CreateVersion7(), token));
    }

    [Fact]
    public async Task AnAdminClearsMany()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await CreateMedia(Constants.USER_ADMIN, 3);

        // an id naming nothing is skipped rather than failing the batch, matching
        // how the bulk set treats one
        var ids = scratch.Media.Select(m => m.Id).Append(Guid.CreateVersion7()).ToArray();

        Assert.True(await GetRepo().BulkClearGpsOverride(Constants.USER_ADMIN, ids, token));

        foreach (var media in scratch.Media)
        {
            var row = await MediaRow(media.Id);

            Assert.Null(row.LocationOverrideId);
            Assert.Equal(media.RecordedLocationId, row.LocationId);
        }
    }

    [Fact]
    public async Task ANonAdminCannotClearMany()
    {
        var token = TestContext.Current.CancellationToken;
        var scratch = await CreateMedia(Constants.USER_ADMIN, 2);

        Assert.False(await GetRepo().BulkClearGpsOverride(
            Constants.USER_JOHNDOE, [.. scratch.Media.Select(m => m.Id)], token));

        foreach (var media in scratch.Media)
        {
            Assert.Equal(media.OverrideLocationId, (await MediaRow(media.Id)).LocationOverrideId);
        }
    }

    // a private category owned by `owner`, holding `count` media that each carry
    // both a recorded location and an override.  built with sql because there is
    // no endpoint that creates media - they arrive from the publisher - and through
    // the setup connection because the service account may not insert a category.
    //
    // the category is given no roles, so no user can see it.  that is what keeps
    // these rows out of every count the rest of the suite asserts on: random media,
    // category listings and place totals all reach media through
    // media.user_category, and nothing reaches this one.
    internal static async Task<Scratch> CreateMedia(TestFixture fixture, Guid owner, int count)
    {
        // the random tail of a v7 guid, not its head, for the reason
        // PlaceAdminTests gives: the head is a timestamp, and parallel classes
        // building scratch data in the same millisecond would collide on slugs
        var tag = Guid.CreateVersion7().ToString("N")[^8..];
        var categoryId = Guid.CreateVersion7();

        await using var conn = fixture.SetupDataSource.CreateConnection();

        await conn.ExecuteAsync(
            """
            INSERT INTO media.category (id, name, slug, effective_date, created, created_by, modified, modified_by)
            VALUES (@categoryId, @name, @slug, '2001-01-01', NOW(), @owner, NOW(), @owner);
            """,
            new { categoryId, name = $"override scratch {tag}", slug = $"override-scratch-{tag}", owner });

        var media = new List<ScratchMedia>();

        for (var i = 0; i < count; i++)
        {
            var m = new ScratchMedia(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

            await conn.ExecuteAsync(
                """
                INSERT INTO media.location (id, latitude, longitude) VALUES
                    (@recorded, @recordedLat, @recordedLon),
                    (@override, @overrideLat, @overrideLon);

                INSERT INTO media.media (id, type_id, location_id, location_override_id, created, created_by, modified, modified_by)
                VALUES (@mediaId, @type, @recorded, @override, NOW(), @owner, NOW(), @owner);

                INSERT INTO media.category_media (category_id, media_id, slug, is_teaser, created, created_by, modified, modified_by)
                VALUES (@categoryId, @mediaId, @slug, false, NOW(), @owner, NOW(), @owner);
                """,
                new
                {
                    mediaId = m.Id,
                    recorded = m.RecordedLocationId,
                    @override = m.OverrideLocationId,
                    type = Constants.TYPE_PHOTO,
                    owner,
                    categoryId,
                    slug = $"media-{i}",
                    // media.location is unique on (latitude, longitude), so each
                    // coordinate is derived from its own row's random tail.  the
                    // southern and western bases keep clear of PlaceAdminTests,
                    // which builds its coordinates in the far north east.
                    recordedLat = -80m - Spread(m.RecordedLocationId, 0),
                    recordedLon = -170m - Spread(m.RecordedLocationId, 1),
                    overrideLat = -70m - Spread(m.OverrideLocationId, 0),
                    overrideLon = -160m - Spread(m.OverrideLocationId, 1)
                });

            media.Add(m);
        }

        return new Scratch(categoryId, media);
    }

    Task<Scratch> CreateMedia(Guid owner, int count) => CreateMedia(_fixture, owner, count);

    // a stable pseudo-random fraction below 9.000000, so a coordinate stays inside
    // NUMERIC(8,6) for latitude and NUMERIC(9,6) for longitude
    static decimal Spread(Guid id, int part)
    {
        var bits = Convert.ToUInt64(id.ToString("N")[^12..], 16);

        return (bits / (ulong)Math.Pow(9_000_000, part) % 9_000_000) / 1_000_000m;
    }

    async Task<MediaLocationRow> MediaRow(Guid id)
    {
        await using var conn = _fixture.DataSource.CreateConnection();

        return await conn.QuerySingleAsync<MediaLocationRow>(
            "SELECT location_id, location_override_id FROM media.media WHERE id = @id",
            new { id });
    }

    async Task<T?> Scalar<T>(string sql, object id)
    {
        await using var conn = _fixture.DataSource.CreateConnection();

        return await conn.ExecuteScalarAsync<T>(sql, new { id });
    }

    MediaRepository GetRepo() =>
        new(
            new FakeLogger<MediaRepository>(),
            _fixture.DataSource.CreateConnection(),
            new FakeHybridCache(),
            new AssetPathBuilder()
        );

    internal record ScratchMedia(Guid Id, Guid RecordedLocationId, Guid OverrideLocationId);

    internal record Scratch(Guid CategoryId, IReadOnlyList<ScratchMedia> Media);

    record MediaLocationRow(Guid? LocationId, Guid? LocationOverrideId);
}
