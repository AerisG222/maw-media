namespace MawMedia.Models;

public record CategoryStat(
    string CategoryName,
    string MediaType,
    long MediaCount,
    decimal FileSize,
    long Duration
);
