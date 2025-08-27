namespace MawMedia.Models;

public record YearStat(
    short Year,
    long CategoryCount,
    IEnumerable<MediaTypeStat> MediaTypeStats
);
