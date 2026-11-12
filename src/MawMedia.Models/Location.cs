namespace MawMedia.Models;

public sealed record Location(
    Guid Id,
    decimal Latitude,
    decimal Longitude
);
