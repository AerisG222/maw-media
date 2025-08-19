namespace MawMedia.Models;

public record Gps(
    Guid MediaId,
    GpsCoordinate? Recorded,
    GpsCoordinate? Override
);
