namespace MawMedia.Models;

public sealed record Gps(
    Guid MediaId,
    GpsCoordinate? Recorded,
    GpsCoordinate? Override
);
