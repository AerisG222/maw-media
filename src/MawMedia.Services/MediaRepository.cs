using System.Text.Json;
using MawMedia.Models;
using MawMedia.Services.Abstractions;
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
        byte count,
        CancellationToken token = default
    )
    {
        var results = await Query<MediaAndFile>(
            "SELECT * FROM media.get_random_media(@userId, @count, @excludeSrcFiles);",
            new
            {
                userId,
                count,
                excludeSrcFiles = true
            },
            token
        );

        return await AssembleMedia(userId, results, baseUrl, _assetPathBuilder, _cache, token);
    }

    public async Task<Media?> GetMedia(Guid userId, string baseUrl, Guid mediaId, CancellationToken token = default)
    {
        var results = await Query<MediaAndFile>(
            "SELECT * FROM media.get_media(@userId, @mediaId, @excludeSrcFiles);",
            new
            {
                userId,
                mediaId,
                excludeSrcFiles = true
            },
            token
        );

        return (await AssembleMedia(userId, results, baseUrl, _assetPathBuilder, _cache, token))
            .SingleOrDefault();
    }

    public async Task<Gps?> GetGps(Guid userId, Guid mediaId, CancellationToken token = default)
    {
        var rec = await QuerySingle<GpsRecord>(
            "SELECT * FROM media.get_media_gps(@userId, @mediaId, NULL);",
            new
            {
                userId,
                mediaId
            },
            token
        );

        return rec?.ToGps();
    }

    public async Task<JsonDocument?> GetMetadata(Guid userId, Guid mediaId, CancellationToken token = default)
    {
        return await RunCommand(async conn =>
        {
            await using var cmd = new NpgsqlCommand("SELECT * FROM media.get_metadata($1, $2);", conn);
            cmd.Parameters.Add(new() { Value = userId });
            cmd.Parameters.Add(new() { Value = mediaId });

            await using var reader = await cmd.ExecuteReaderAsync(token);

            if (await reader.ReadAsync(token))
            {
                return reader.IsDBNull(0)
                    ? null
                    : reader.GetFieldValue<JsonDocument>(0);
            }

            return null;
        }, token);
    }

    public async Task<Media?> SetIsFavorite(Guid userId, string baseUrl, Guid mediaId, bool isFavorite, CancellationToken token = default)
    {
        var result = await ExecuteScalarInTransaction<int>(
            "SELECT * FROM media.favorite_media(@userId, @mediaId, @isFavorite);",
            new
            {
                userId,
                mediaId,
                isFavorite
            },
            token
        );

        if (result == 0)
        {
            return await GetMedia(userId, baseUrl, mediaId, token);
        }

        _log.LogWarning("Unable to set media favorite - user {USER} does not have access to media {MEDIA} - or media does not exist!", userId, mediaId);

        return null;
    }

    public async Task<IEnumerable<Comment>> GetComments(Guid userId, Guid mediaId, CancellationToken token = default)
    {
        return await InternalGetComments(userId, mediaId, null, token);
    }

    public async Task<Comment?> GetComment(Guid userId, Guid commentId, CancellationToken token = default)
    {
        var comments = await InternalGetComments(userId, null, commentId, token);

        return comments
            .SingleOrDefault();
    }

    public async Task<Guid?> AddComment(Guid userId, Guid mediaId, string body, CancellationToken token = default)
    {
        var commentId = Guid.CreateVersion7();

        var result = await ExecuteScalarInTransaction<int>(
            "SELECT * FROM media.add_comment(@commentId, @userId, @mediaId, @body);",
            new
            {
                commentId,
                userId,
                mediaId,
                body
            },
            token
        );

        if (result == 0)
        {
            return commentId;
        }

        _log.LogWarning("Unable to add comment - user {USER} does not have access to media {MEDIA} - or media does not exist!", userId, mediaId);

        return null;
    }

    public async Task<MediaFile?> GetMediaFile(Guid userId, Guid assetId, CancellationToken token = default)
    {
        var file = await InternalGetMediaFile(userId, assetId, null, token);

        if (file == null)
        {
            _log.LogWarning("Unable to get media file - user {USER} does not have access to asset {ASSET} - or asset does not exist!", userId, assetId);
        }

        return file;
    }

    public async Task<MediaFile?> GetMediaFile(Guid userId, string path, CancellationToken token = default)
    {
        var file = await InternalGetMediaFile(userId, null, path, token);

        if (file == null)
        {
            _log.LogWarning("Unable to get media file - user {USER} does not have access to asset {ASSET} - or asset does not exist!", userId, path);
        }

        return file;
    }

    public async ValueTask<bool> AllowAccessToAsset(Guid userId, string path, CancellationToken token) =>
        await _cache.GetOrCreateAsync(
            CacheKeyBuilder.CanAccessAsset(userId, path),
            async cancel => (await InternalGetMediaFile(userId, null, path, cancel)) != null,
            cancellationToken: token
        );

    public async Task<bool> SetGpsOverride(Guid userId, Guid mediaId, Guid newLocationId, decimal latitude, decimal longitude, CancellationToken token = default)
    {
        var result = await ExecuteScalarInTransaction<int>(
            "SELECT media.set_media_gps_override(@userId, @mediaId, @newLocationId, @latitude, @longitude);",
            new
            {
                userId,
                mediaId,
                newLocationId,
                latitude,
                longitude
            },
            token
        );

        return result == 0;
    }

    public async Task<bool> BulkSetGpsOverride(Guid userId, Guid[] mediaIds, Guid newLocationId, decimal latitude, decimal longitude, CancellationToken token = default)
    {
        var result = await ExecuteScalarInTransaction<int>(
            "SELECT media.bulk_set_media_gps_override(@userId, @mediaIds, @newLocationId, @latitude, @longitude);",
            new
            {
                userId,
                mediaIds,
                newLocationId,
                latitude,
                longitude
            },
            token
        );

        return result == 0;
    }

    async Task<MediaFile?> InternalGetMediaFile(Guid userId, Guid? assetId, string? path, CancellationToken token = default) =>
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
            },
            token
        );

    async Task<IEnumerable<Comment>> InternalGetComments(Guid userId, Guid? mediaId, Guid? commentId, CancellationToken token = default)
    {
        return await Query<Comment>(
            "SELECT * FROM media.get_comments(@userId, @mediaId, @commentId);",
            new
            {
                userId,
                mediaId,
                commentId
            },
            token
        );
    }
}
