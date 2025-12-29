using NodaTime;

namespace MawMedia.Models;

public record LocationMetadata(
    Guid LocationId,
    Instant LookupDate,
    string? FormattedAddress,
    string? AdministrativeAreaLevel1,
    string? AdministrativeAreaLevel2,
    string? AdministrativeAreaLevel3,
    string? Country,
    string? Locality,
    string? Neighborhood,
    string? SubLocalityLevel1,
    string? SubLocalityLevel2,
    string? PostalCode,
    string? PostalCodeSuffix,
    string? Premise,
    string? Route,
    string? StreetNumber,
    string? SubPremise
);
