using Dapper;
using MawMedia.Services;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace MawMedia.Services.Tests;

// merging and re-parenting are destructive - a merge deletes a place outright -
// so every subject here is created by the test and never drawn from the seeded
// tree, for exactly the reason ClanTests gives: test classes run in parallel
// against one database, and a shared subject would be a flake waiting to happen.
//
// the seeded places are read by half the suite, so folding one away would break
// tests that have nothing to do with this file.
public class PlaceAdminTests
{
    readonly TestFixture _fixture;

    public PlaceAdminTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task MergingRepointsLocationsChildrenAndAliases()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var scratch = await CreateBranch();

        var outcome = await repo.MergePlaces(Constants.USER_ADMIN, scratch.KeepState, scratch.FoldState, token);

        Assert.Equal(PlaceAdminOutcome.Ok, outcome);

        // the folded place is gone
        Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM media.place WHERE id = @id", scratch.FoldState));

        // its child came across rather than being orphaned or deleted
        Assert.Equal(scratch.KeepState,
            await Scalar<Guid>("SELECT parent_id FROM media.place WHERE id = @id", scratch.FoldCity));

        // so did its coordinates
        Assert.Equal(scratch.KeepState,
            await Scalar<Guid>("SELECT place_id FROM media.location WHERE id = @id", scratch.FoldLocation));

        // and its alias, which is what stops the next derivation pass recreating
        // the place that was just merged away
        Assert.Equal(scratch.KeepState,
            await Scalar<Guid>("SELECT place_id FROM media.place_alias WHERE match_key = @id", scratch.FoldKey));
    }

    [Fact]
    public async Task MergingSurvivesRederivation()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var scratch = await CreateBranch();

        await repo.MergePlaces(Constants.USER_ADMIN, scratch.KeepState, scratch.FoldState, token);

        await using var conn = _fixture.DataSource.CreateConnection();
        await conn.ExecuteScalarAsync<int>("SELECT media.assign_all_location_places();");

        // the whole point of routing the correction through media.place_alias: the
        // geocode text that produced the folded place still exists on the location,
        // and derivation must resolve it to the survivor rather than build it again
        Assert.Equal(0, await Scalar<int>(
            "SELECT COUNT(*) FROM media.place WHERE id = @id", scratch.FoldState));
        Assert.Equal(scratch.KeepState,
            await Scalar<Guid>("SELECT place_id FROM media.location WHERE id = @id", scratch.FoldLocation));
    }

    [Fact]
    public async Task MergingIsRefusedAcrossKindsIntoItselfAndForUnknownPlaces()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var scratch = await CreateBranch();

        Assert.Equal(PlaceAdminOutcome.SamePlace,
            await repo.MergePlaces(Constants.USER_ADMIN, scratch.Country, scratch.Country, token));
        Assert.Equal(PlaceAdminOutcome.KindMismatch,
            await repo.MergePlaces(Constants.USER_ADMIN, scratch.Country, scratch.KeepState, token));
        Assert.Equal(PlaceAdminOutcome.NotFound,
            await repo.MergePlaces(Constants.USER_ADMIN, scratch.Country, Guid.CreateVersion7(), token));

        // a reader may not reshape a tree everyone else browses
        Assert.Equal(PlaceAdminOutcome.NotAdmin,
            await repo.MergePlaces(Constants.USER_JOHNDOE, scratch.KeepState, scratch.FoldState, token));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM media.place WHERE id = @id", scratch.FoldState));
    }

    [Fact]
    public async Task ReParentingMovesAPlaceAndLeavesItsAliasAlone()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var scratch = await CreateBranch();

        var outcome = await repo.SetPlaceParent(Constants.USER_ADMIN, scratch.FoldCity, scratch.KeepState, token);

        Assert.Equal(PlaceAdminOutcome.Ok, outcome);
        Assert.Equal(scratch.KeepState,
            await Scalar<Guid>("SELECT parent_id FROM media.place WHERE id = @id", scratch.FoldCity));

        // the alias deliberately still records what the geocoder says - the old
        // parent - because that is what the next lookup will ask about.  moving it
        // would make the lookup miss and quietly recreate the place under the
        // parent it was just moved out of.
        Assert.Equal(scratch.FoldState,
            await Scalar<Guid>("SELECT parent_place_id FROM media.place_alias WHERE place_id = @id", scratch.FoldCity));

        await using var conn = _fixture.DataSource.CreateConnection();
        await conn.ExecuteScalarAsync<int>("SELECT media.assign_all_location_places();");

        Assert.Equal(scratch.KeepState,
            await Scalar<Guid>("SELECT parent_id FROM media.place WHERE id = @id", scratch.FoldCity));
    }

    [Fact]
    public async Task ReParentingIsRefusedWhenTheParentDoesNotSitAbove()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var scratch = await CreateBranch();

        // a state beneath a city, a country anywhere but the root, and a city
        // promoted to the root are all rejected
        Assert.Equal(PlaceAdminOutcome.InvalidParent,
            await repo.SetPlaceParent(Constants.USER_ADMIN, scratch.KeepState, scratch.FoldCity, token));
        Assert.Equal(PlaceAdminOutcome.InvalidParent,
            await repo.SetPlaceParent(Constants.USER_ADMIN, scratch.Country, scratch.KeepState, token));
        Assert.Equal(PlaceAdminOutcome.NotARootKind,
            await repo.SetPlaceParent(Constants.USER_ADMIN, scratch.FoldCity, null, token));
        Assert.Equal(PlaceAdminOutcome.NotFound,
            await repo.SetPlaceParent(Constants.USER_ADMIN, Guid.CreateVersion7(), scratch.KeepState, token));

        Assert.Equal(scratch.Country,
            await Scalar<Guid>("SELECT parent_id FROM media.place WHERE id = @id", scratch.KeepState));
    }

    // a private country/state/city branch with its own coordinate and aliases.
    // built with sql rather than through the api because there is no endpoint that
    // creates a place - the tree is derived, and these are the corrections applied
    // to it afterwards.
    async Task<Branch> CreateBranch()
    {
        // taken from the *random* tail of the guid, not its head.  a v7 guid begins
        // with a millisecond timestamp, and these classes run in parallel - two
        // branches built in the same millisecond would take the same tag and
        // collide on the per-parent slug index.
        // taken from the *random* tail of the guid, not its head.  a v7 guid begins
        // with a millisecond timestamp, and these classes run in parallel - two
        // branches built in the same millisecond would take the same tag and
        // collide on the per-parent slug index.
        var tag = Guid.CreateVersion7().ToString("N")[^8..];

        // the alias keys have to be exactly what media.normalize_place_name would
        // produce from the location's own text, or a derivation pass will not match
        // them and will build a parallel branch instead - which is precisely what
        // MergingSurvivesRederivation is checking, so getting this wrong makes the
        // test pass for the wrong reason or fail for a fixture bug.
        var countryName = $"{tag} country";
        var foldName = $"{tag} fold";

        var b = new Branch(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), Guid.CreateVersion7(), foldName);

        await using var conn = _fixture.DataSource.CreateConnection();

        await conn.ExecuteAsync(
            """
            INSERT INTO media.place (id, parent_id, kind, name, slug, created, modified) VALUES
                (@country,   NULL,       'country', @countryName,     @tag || '-country', NOW(), NOW()),
                (@keepState, @country,   'state',   @tag || ' keep',  @tag || '-keep',    NOW(), NOW()),
                (@foldState, @country,   'state',   @foldName,        @tag || '-fold',    NOW(), NOW()),
                (@foldCity,  @foldState, 'city',    @tag || ' city',  @tag || '-city',    NOW(), NOW());

            INSERT INTO media.place_alias (place_id, kind, parent_place_id, match_key, created) VALUES
                (@country,   'country', NULL,       @countryName,       NOW()),
                (@keepState, 'state',   @country,   @tag || ' keep',    NOW()),
                (@foldState, 'state',   @country,   @foldName,          NOW()),
                (@foldCity,  'city',    @foldState, @tag || ' city',    NOW());

            INSERT INTO media.location (id, latitude, longitude, country, administrative_area_level_1, place_id)
            VALUES (@foldLocation, @lat, @lon, @countryName, @foldName, @foldState);
            """,
            new
            {
                country = b.Country,
                keepState = b.KeepState,
                foldState = b.FoldState,
                foldCity = b.FoldCity,
                foldLocation = b.FoldLocation,
                countryName,
                foldName,
                tag,
                // media.location is keyed on (latitude, longitude), so a branch
                // needs a coordinate no other one takes.  derived from the random
                // tail of the location id for the same reason the tag is.
                lat = 80m + Spread(b.FoldLocation, 0),
                lon = 170m + Spread(b.FoldLocation, 1)
            });

        return b;
    }

    // a stable pseudo-random fraction below 9.000000, so a coordinate stays inside
    // NUMERIC(8,6) for latitude and NUMERIC(9,6) for longitude
    static decimal Spread(Guid id, int part)
    {
        var bits = Convert.ToUInt64(id.ToString("N")[^12..], 16);

        return (bits / (ulong)Math.Pow(9_000_000, part) % 9_000_000) / 1_000_000m;
    }

    async Task<T?> Scalar<T>(string sql, object id)
    {
        await using var conn = _fixture.DataSource.CreateConnection();

        return await conn.ExecuteScalarAsync<T>(sql, new { id });
    }

    PlaceRepository GetRepo() =>
        new(
            new FakeLogger<PlaceRepository>(),
            _fixture.DataSource.CreateConnection(),
            new AssetPathBuilder(),
            new FakeHybridCache(),
            new PlaceCoverStore(
                new FakeLogger<PlaceCoverStore>(),
                Options.Create(new PlaceCoverConfig { RootDirectory = CoverDir() }),
                Options.Create(new AssetConfig { RootDirectory = AssetDir() }))
        );

    static string CoverDir() => EnsureDir("place-admin-covers");
    static string AssetDir() => EnsureDir("place-admin-assets");

    static string EnsureDir(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "maw-media-place-admin", name);

        Directory.CreateDirectory(path);

        return path;
    }

    record Branch(Guid Country, Guid KeepState, Guid FoldState, Guid FoldCity, Guid FoldLocation, string FoldKey);
}
