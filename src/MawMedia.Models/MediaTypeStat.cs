namespace MawMedia.Models;

public record MediaTypeStat(
    string MediaType,
    long MediaCount,
    decimal FileSize,
    float? Duration
);
