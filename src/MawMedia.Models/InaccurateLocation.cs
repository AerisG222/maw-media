namespace MawMedia.Models;

public record InaccurateLocation(
    Guid MediaId,
    decimal ExifLatitude,
    decimal ExifLongitude,
    Guid MappedLocationId,
    decimal MappedLatitude,
    decimal MappedLongitude
);
