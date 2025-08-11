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
        return await Query<YearStat>(
            "SELECT * FROM media.get_stats(@userId);",
            new
            {
                userId
            }
        );
    }

    public async Task<IEnumerable<CategoryStat>> GetStatsForYear(Guid userId, short year)
    {
        return await Query<CategoryStat>(
            "SELECT * FROM media.get_stats_for_year(@userId, @year);",
            new
            {
                userId,
                year
            }
        );
    }
}
