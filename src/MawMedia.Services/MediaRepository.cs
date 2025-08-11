using System.Text.Json;
using MawMedia.Models;
using MawMedia.Services.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MawMedia.Services;

public class MediaRepository
    : BaseRepository, IMediaRepository
{
    readonly HybridCache _cache;
    readonly IAssetPathBuilder _assetPathBuilder;

    public MediaRepository(
        ILogger<MediaRepository> log,
        NpgsqlConnection conn,
        HybridCache cache,
        IAssetPathBuilder assetPathBuilder
    ) : base(log, conn)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(assetPathBuilder);

        _cache = cache;
        _assetPathBuilder = assetPathBuilder;
    }

    public async Task<IEnumerable<Media>> GetRandomMedia(
        Guid userId,
        string baseUrl,
        byte count
    )
    {
        var results = await Query<MediaAndFile>(
            "SELECT * FROM media.get_random_media(@userId, @count, @excludeSrcFiles);",
            new
            {
                userId,
                count,
                excludeSrcFiles = true
            }
        );

        return await AssembleMedia(userId, results, baseUrl, _assetPathBuilder, _cache);
    }

    public async Task<Media?> GetMedia(Guid userId, string baseUrl, Guid mediaId)
    {
        var results = await Query<MediaAndFile>(
            "SELECT * FROM media.get_media(@userId, @mediaId, @excludeSrcFiles);",
            new
            {
                userId,
                mediaId,
                excludeSrcFiles = true
            }
        );

        return (await AssembleMedia(userId, results, baseUrl, _assetPathBuilder, _cache))
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

    public async Task<Media?> SetIsFavorite(Guid userId, string baseUrl, Guid mediaId, bool isFavorite)
    {
        var result = await ExecuteTransaction<int>(
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
            return await GetMedia(userId, baseUrl, mediaId);
        }

        _log.LogWarning("Unable to set media favorite - user {USER} does not have access to media {MEDIA} - or media does not exist!", userId, mediaId);

        return null;
    }

    public async Task<IEnumerable<Comment>> GetComments(Guid userId, Guid mediaId)
    {
        return await InternalGetComments(userId, mediaId, null);
    }

    public async Task<Comment?> GetComment(Guid userId, Guid commentId)
    {
        var comments = await InternalGetComments(userId, null, commentId);

        return comments
            .SingleOrDefault();
    }

    public async Task<Guid?> AddComment(Guid userId, Guid mediaId, string body)
    {
        var commentId = Guid.CreateVersion7();

        var result = await ExecuteTransaction<int>(
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

    public async Task<bool> AllowAccessToAsset(Guid userId, string path, CancellationToken token) =>
        await _cache.GetOrCreateAsync(
            CacheKeyBuilder.CanAccessAsset(userId, path),
            async cancel => (await GetMediaFile(userId, path)) != null,
            cancellationToken: token
        );

    public async Task<bool> SetGpsOverride(Guid userId, Guid mediaId, Guid newLocationId, decimal latitude, decimal longitude)
    {
        var result = await ExecuteTransaction<int>(
            "SELECT media.set_media_gps_override(@userId, @mediaId, @newLocationId, @latitude, @longitude);",
            new
            {
                userId,
                mediaId,
                newLocationId,
                latitude,
                longitude
            }
        );

        return result == 0;
    }

    async Task<MediaFile?> InternalGetMediaFile(Guid userId, Guid? assetId, string? path) =>
        await QuerySingle<MediaFile>(
            """
            SELECT
                file_id AS id,
                file_scale AS scale,
                file_type AS type,
                file_path AS path
            FROM media.get_media_file(@userId, @assetId, @path, @excludeSrcFiles);
            """,
            new
            {
                userId,
                assetId,
                path,
                excludeSrcFiles = true
            }
        );

    async Task<IEnumerable<Comment>> InternalGetComments(Guid userId, Guid? mediaId, Guid? commentId)
    {
        return await Query<Comment>(
            "SELECT * FROM media.get_comments(@userId, @mediaId, @commentId);",
            new
            {
                userId,
                mediaId,
                commentId
            }
        );
    }
}
