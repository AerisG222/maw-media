namespace MawMedia.Models;

public record YearStat(
    short Year,
    string MediaType,
    long MediaCount,
    decimal FileSize,
    long Duration
);
