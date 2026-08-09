using Dapper;
using NodaTime;
using Microsoft.Extensions.Logging.Testing;

namespace MawMedia.Services.Tests;

public class LocationRepositoryTests
{
    readonly TestFixture _fixture;

    public LocationRepositoryTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    public static TheoryData<Guid, bool> LocationsWithoutMetadataData => new()
    {
        { Guid.CreateVersion7(),  false },
        { Constants.USER_JOHNDOE, false },
        { Constants.USER_ADMIN,   true },
    };

    [Theory]
    [MemberData(nameof(LocationsWithoutMetadataData))]
    public async Task GetLocationsWithoutMetadata(Guid userId, bool isAdmin)
    {
        var repo = GetRepo();

        var result = await repo.GetLocationsWithoutMetadata(userId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);

        if (!isAdmin)
        {
            Assert.Empty(result);

            return;
        }

        // asserted by identity rather than by count: test classes run in parallel and
        // media.set_media_gps_override inserts a location with no lookup_date, so the
        // number of un-geocoded locations is not stable across a run.
        Assert.Contains(result, l => l.Id == Constants.LOCATION_UNK.Id);
        Assert.DoesNotContain(result, l => l.Id == Constants.LOCATION_MA.Id);
        Assert.DoesNotContain(result, l => l.Id == Constants.LOCATION_NY.Id);
    }

    [Fact]
    public async Task SetLocation()
    {
        var newId = Guid.CreateVersion7();
        using var conn = _fixture.DataSource.CreateConnection();
        var repo = GetRepo();

        await conn.ExecuteAsync(
            """
            INSERT INTO media.location (id, latitude, longitude)
            VALUES (@newId, @latitude, @longitude);
            """,
            new
            {
                newId,
                latitude = 1m,
                longitude = 1m
            }
        );

        var result = await repo.SetLocationMetadata(Constants.USER_ADMIN, new(
            newId,
            Instant.FromDateTimeUtc(DateTime.UtcNow),
            "FormattedAddress",
            "AdministrativeAreaLevel1",
            "AdministrativeAreaLevel2",
            "AdministrativeAreaLevel3",
            "Country",
            "Locality",
            "Neighborhood",
            "SubLocalityLevel1",
            "SubLocalityLevel2",
            "PostalCode",
            "PostalCodeSuffix",
            "Premise",
            "Route",
            "StreetNumber",
            "SubPremise",
            [
                new("typeA", "nameA"),
                new("typeB", "nameB"),
            ]
        ), TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    LocationRepository GetRepo()
    {
        return new LocationRepository(
            new FakeLogger<LocationRepository>(),
            _fixture.DataSource.CreateConnection()
        );
    }
}
