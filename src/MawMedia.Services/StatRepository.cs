using MawMedia.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MawMedia.Services;

public class StatRepository
    : BaseRepository, IStatRepository
{
    public StatRepository(
        ILogger<StatRepository> log,
        NpgsqlConnection conn
    ) : base(log, conn)
    {

    }

    public async Task<IEnumerable<YearStat>> GetStats(Guid userId)
    {
        var stats = await Query<YearStatRecord>(
            "SELECT * FROM media.get_stats(@userId);",
            new
            {
                userId
            }
        );

        return stats
            .GroupBy(s => s.Year)
            .Select(g => new YearStat(
                g.Key,
                g.First().CategoryCount,
                g.Select(x => new MediaTypeStat(
                    x.MediaType,
                    x.MediaCount,
                    x.FileSize,
                    x.Duration
                ))
            ));
    }

    public async Task<IEnumerable<CategoryStat>> GetStatsForYear(Guid userId, short year)
    {
        var stats = await Query<CategoryStatRecord>(
            "SELECT * FROM media.get_stats_for_year(@userId, @year);",
            new
            {
                userId,
                year
            }
        );

        return stats
            .GroupBy(s => s.CategoryId)
            .Select(g => new CategoryStat(
                g.First().CategoryId,
                g.First().CategoryName,
                g.Select(x => new MediaTypeStat(
                    x.MediaType,
                    x.MediaCount,
                    x.FileSize,
                    x.Duration
                ))
            ));
    }
}
