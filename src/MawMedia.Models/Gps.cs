namespace MawMedia.Models;

public record Gps(
    Guid MediaId,
    decimal Latitude,
    decimal Longitude
);
