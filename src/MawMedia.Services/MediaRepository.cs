using System.Text.Json;
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

    public async Task<Gps?> GetGps(Guid userId, Guid mediaId)
    {
        return await QuerySingle<Gps>(
            "SELECT * FROM media.get_media_gps(@userId, @mediaId, NULL);",
            new
            {
                userId,
                mediaId
            }
        );
    }

    public async Task<JsonDocument?> GetMetadata(Guid userId, Guid mediaId)
    {
        return await RunCommand(async conn =>
        {
            await using var cmd = new NpgsqlCommand("SELECT * FROM media.get_metadata($1, $2);", conn)
            {
                Parameters =
                {
                    new() { Value = userId },
                    new() { Value = mediaId }
                }
            };

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return reader.IsDBNull(0)
                    ? null
                    : reader.GetFieldValue<JsonDocument>(0);
            }

            return null;
        });
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

        if (result == 0)
        {
            return await GetMedia(userId, mediaId);
        }

        _log.LogWarning("Unable to set media favorite - user {USER} does not have access to media {MEDIA} - or media does not exist!", userId, mediaId);

        return null;
    }

    public async Task<IEnumerable<Comment>> GetComments(Guid userId, Guid mediaId)
    {
        return await Query<Comment>(
            "SELECT * FROM media.get_comments(@userId, @mediaId);",
            new
            {
                userId,
                mediaId
            }
        );
    }

    public async Task<Guid?> AddComment(Guid userId, Guid mediaId, string body)
    {
        var commentId = Guid.CreateVersion7();

        var result = await ExecuteTransaction(
            "SELECT * FROM media.add_comment(@commentId, @userId, @mediaId, @body);",
            new
            {
                commentId,
                userId,
                mediaId,
                body
            }
        );

        if (result == 0)
        {
            return commentId;
        }

        _log.LogWarning("Unable to add comment - user {USER} does not have access to media {MEDIA} - or media does not exist!", userId, mediaId);

        return null;
    }

    public async Task<MediaFile?> GetMediaFile(Guid userId, Guid assetId)
    {
        var file = await InternalGetMediaFile(userId, assetId, null);

        if (file == null)
        {
            _log.LogWarning("Unable to get media file - user {USER} does not have access to asset {ASSET} - or asset does not exist!", userId, assetId);
        }

        return file;
    }

    public async Task<MediaFile?> GetMediaFile(Guid userId, string path)
    {
        var file = await InternalGetMediaFile(userId, null, path);

        if (file == null)
        {
            _log.LogWarning("Unable to get media file - user {USER} does not have access to asset {ASSET} - or asset does not exist!", userId, path);
        }

        return file;
    }

    async Task<MediaFile?> InternalGetMediaFile(Guid userId, Guid? assetId, string? path) =>
        await QuerySingle<MediaFile>(
            """
            SELECT
                file_id AS id,
                file_scale AS scale,
                file_type AS type,
                file_path AS path
            FROM media.get_media_file(@userId, @assetId, @path);
            """,
            new
            {
                userId,
                assetId,
                path
            }
        );
}
