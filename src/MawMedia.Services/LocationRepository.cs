using Dapper;
using MawMedia.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MawMedia.Services;

public class LocationRepository
    : BaseRepository, ILocationRepository
{
    public LocationRepository(
        ILogger<LocationRepository> log,
        NpgsqlConnection conn
    ) : base(log, conn)
    {

    }

    public async Task<IEnumerable<Location>> GetLocationsWithoutMetadata(Guid userId) =>
        await Query<Location>(
            "SELECT * FROM media.get_locations_without_metadata(@userId);",
            new
            {
                userId
            }
        );

    public async Task<bool> SetLocationMetadata(Guid userId, LocationMetadata metadata)
    {
        return await RunTransaction(
            async conn => {
                var result = await conn.ExecuteScalarAsync<int>(
                    """
                    SELECT * FROM media.set_location_metadata(
                        @userId,
                        @locationId,
                        @lookupDate,
                        @formattedAddress,
                        @administrativeAreaLevel1,
                        @administrativeAreaLevel2,
                        @administrativeAreaLevel3,
                        @country,
                        @locality,
                        @neighborhood,
                        @subLocalityLevel1,
                        @subLocalityLevel2,
                        @postalCode,
                        @postalCodeSuffix,
                        @premise,
                        @route,
                        @streetNumber,
                        @subPremise
                    );
                    """,
                    new
                    {
                        userId,
                        locationId = metadata.LocationId,
                        lookupDate = metadata.LookupDate,
                        formattedAddress = metadata.FormattedAddress,
                        administrativeAreaLevel1 = metadata.AdministrativeAreaLevel1,
                        administrativeAreaLevel2 = metadata.AdministrativeAreaLevel2,
                        administrativeAreaLevel3 = metadata.AdministrativeAreaLevel3,
                        country = metadata.Country,
                        locality = metadata.Locality,
                        neighborhood = metadata.Neighborhood,
                        subLocalityLevel1 = metadata.SubLocalityLevel1,
                        subLocalityLevel2 = metadata.SubLocalityLevel2,
                        postalCode = metadata.PostalCode,
                        postalCodeSuffix = metadata.PostalCodeSuffix,
                        premise = metadata.Premise,
                        route = metadata.Route,
                        streetNumber = metadata.StreetNumber,
                        subPremise = metadata.SubPremise
                    }
                );

                switch(result)
                {
                    case 1:
                        _log.LogWarning("Unable to set location metadata: user {USER} is not an admin!", userId);
                        throw new ApplicationException();
                    case 2:
                        _log.LogWarning("Unable to set location metadata: location {LOCATION} does not exist!", metadata.LocationId);
                        throw new ApplicationException();
                }

                foreach(var poi in metadata.PointsOfInterest)
                {
                    result = await conn.ExecuteScalarAsync<int>(
                        """
                        SELECT * FROM media.set_point_of_interest(
                            @userId,
                            @locationId,
                            @type,
                            @name
                        );
                        """,
                        new
                        {
                            userId,
                            locationId = metadata.LocationId,
                            type = poi.Type,
                            name = poi.Name
                        }
                    );

                    if(result != 0)
                    {
                        throw new ApplicationException("Unable to set location metadata: unable to set POI!");
                    }
                }

                return true;
            }
        );
    }
}
