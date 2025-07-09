using Microsoft.Extensions.Logging;
using Npgsql;
using MawMedia.Models;

namespace MawMedia.Services;

public class ConfigRepository
    : BaseRepository, IConfigRepository
{
    public ConfigRepository(
        ILogger<ConfigRepository> log,
        NpgsqlConnection conn
    ) : base(log, conn)
    {

    }

    public async Task<IEnumerable<Scale>> GetScales() =>
        await Query<Scale>("SELECT * FROM media.get_scales();");
}
