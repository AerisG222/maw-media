using MawMedia.Models;

namespace MawMedia.Services.Abstractions;

public interface IStatRepository
{
    Task<IEnumerable<YearStat>> GetStats(Guid userId, CancellationToken token = default);
    Task<IEnumerable<CategoryStat>> GetStatsForYear(Guid userId, short year, CancellationToken token = default);
}
