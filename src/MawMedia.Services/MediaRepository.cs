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

    public async Task<Media?> GetMedia(Guid userId, Guid mediaId)
    {
        var results = await Query<MediaAndFile>(
            "SELECT * FROM media.get_media(@userId, @mediaId);",
            new
            {
                userId,
                mediaId
            }
        );

        return AssembleMedia(results)
            .SingleOrDefault();
    }

    public async Task<Media?> SetIsFavorite(Guid userId, Guid mediaId, bool isFavorite)
    {
        var result = await ExecuteTransaction(
            "SELECT * FROM media.favorite_media(@userId, @mediaId, @isFavorite);",
            new
            {
                userId,
                mediaId,
                isFavorite
            }
        );

        if(result == 0)
        {
            return await GetMedia(userId, mediaId);
        }

        _log.LogWarning("Unable to set media favorite - user {USER} does not have access to media {MEDIA} - or media does not exist!", userId, mediaId);

        return null;
    }
}
