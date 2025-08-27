namespace MawMedia.Models;

public record CategoryStat(
    Guid? CategoryId,
    string CategoryName,
    IEnumerable<MediaTypeStat> MediaTypeStats
);
