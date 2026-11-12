namespace MawMedia.Services.Models;

public sealed record CategoryStatRecord(
    Guid? CategoryId,
    string CategoryName,
    string MediaType,
    long MediaCount,
    decimal FileSize,
    float? Duration
);
