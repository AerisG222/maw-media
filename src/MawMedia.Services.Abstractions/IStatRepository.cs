using MawMedia.Models;

namespace MawMedia.Services.Abstractions;

public interface IStatRepository
{
    Task<IEnumerable<YearStat>> GetStats(Guid userId);
    Task<IEnumerable<CategoryStat>> GetStatsForYear(Guid userId, short year);
}
