namespace MawMedia.Models;

public sealed record YearStat(
    short Year,
    long CategoryCount,
    IEnumerable<MediaTypeStat> MediaTypeStats
);
