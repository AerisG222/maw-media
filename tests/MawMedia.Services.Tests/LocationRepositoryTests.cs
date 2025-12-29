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

    public static TheoryData<Guid, int> LocationsWithoutMetadataData => new()
    {
        { Guid.CreateVersion7(),  0 },
        { Constants.USER_JOHNDOE, 0 },
        { Constants.USER_ADMIN,   1 },
    };

    [Theory]
    [MemberData(nameof(LocationsWithoutMetadataData))]
    public async Task GetLocationsWithoutMetadata(Guid userId, int count)
    {
        var repo = GetRepo();

        var result = await repo.GetLocationsWithoutMetadata(userId);

        Assert.NotNull(result);
        Assert.Equal(count, result.Count());
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
        ));

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
