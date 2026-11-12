namespace MawMedia.Models;

public sealed record MediaTypeStat(
    string MediaType,
    long MediaCount,
    decimal FileSize,
    float? Duration
);
