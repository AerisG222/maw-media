namespace MawMedia.Models;

public sealed record CategoryStat(
    Guid? CategoryId,
    string CategoryName,
    IEnumerable<MediaTypeStat> MediaTypeStats
);
