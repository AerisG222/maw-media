namespace MawMedia.Models;

public sealed record InaccurateLocation(
    Guid MediaId,
    decimal ExifLatitude,
    decimal ExifLongitude,
    Guid MappedLocationId,
    decimal MappedLatitude,
    decimal MappedLongitude
);
