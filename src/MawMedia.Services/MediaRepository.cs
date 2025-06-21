using MawMedia.Models;
using MawMedia.Services.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MawMedia.Services;

public class MediaRepository
    : BaseRepository, IMediaRepository
{
    public MediaRepository(
        ILogger<MediaRepository> log,
        NpgsqlConnection conn
    ) : base(log, conn)
    {

    }

    public async Task<IEnumerable<Media>> GetRandomMedia(
        Guid userId,
        byte count
    )
    {
        var results = await Query<MediaAndFile>(
            "SELECT * FROM media.get_random_media(@userId, @count);",
            new
            {
                userId,
                count
            }
        );

        return AssembleMedia(results);
    }
}
