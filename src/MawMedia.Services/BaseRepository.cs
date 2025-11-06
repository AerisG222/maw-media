using Dapper;
using MawMedia.Models;
using MawMedia.Services.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MawMedia.Services;

public class BaseRepository
{
    readonly NpgsqlConnection _conn;
    protected readonly ILogger _log;

    public BaseRepository(
        ILogger log,
        NpgsqlConnection conn
    )
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(conn);

        _log = log;
        _conn = conn;
    }

    protected async Task Execute(
        string statement,
        object? param = null
    )
    {
        await RunCommand(
            conn =>
                conn.ExecuteAsync(
                    statement,
                    param
                )
        );
    }

    protected async Task<T?> ExecuteScalarInTransaction<T>(
        string statement,
        object? param = null
    )
    {
        return await RunTransaction(
            conn =>
                conn.ExecuteScalarAsync<T>(
                    statement,
                    param
                )
        );
    }

    protected async Task<IEnumerable<T>?> ExecuteQueryInTransaction<T>(
        string statement,
        object? param = null
    )
    {
        return await RunTransaction(
            conn =>
                conn.QueryAsync<T>(
                    statement,
                    param
                )
        );
    }

    protected async Task<T?> ExecuteScalar<T>(
        string statement,
        object? param = null
    )
    {
        return await RunCommand(
            conn =>
                conn.ExecuteScalarAsync<T>(
                    statement,
                    param
                )
        );
    }

    protected async Task<IEnumerable<T>> Query<T>(
        string statement,
        object? param = null
    )
    {
        return await RunCommand(
            conn =>
                conn.QueryAsync<T>(
                    statement,
                    param
                )
        );
    }

    protected async Task<T?> QuerySingle<T>(
        string statement,
        object? param = null
    )
    {
        return await RunCommand(
            conn =>
                conn.QuerySingleOrDefaultAsync<T>(
                    statement,
                    param
                )
        );
    }

    protected async Task<T> RunCommand<T>(Func<NpgsqlConnection, Task<T>> command)
    {
        try
        {
            await _conn.OpenAsync();

            return await command(_conn);
        }
        finally
        {
            if (_conn != null)
            {
                await _conn.CloseAsync();
            }
        }
    }

    protected async Task<T?> RunTransaction<T>(Func<NpgsqlConnection, Task<T>> command, bool runInTransaction = false)
    {
        NpgsqlTransaction? tran = null;

        try
        {
            await _conn.OpenAsync();
            tran = await _conn.BeginTransactionAsync();

            var result = await command(_conn);

            await tran.CommitAsync();

            return result;
        }
        catch
        {
            if (tran != null)
            {
                await tran.RollbackAsync();
            }

            throw;
        }
        finally
        {
            if (tran != null)
            {
                await tran.DisposeAsync();
            }

            if (_conn != null)
            {
                await _conn.CloseAsync();
            }
        }
    }

    internal static async Task<IEnumerable<Media>> AssembleMedia(
        Guid userId,
        IEnumerable<MediaAndFile> mediaAndFiles,
        string baseUrl,
        IAssetPathBuilder assetPathBuilder,
        HybridCache cache
    )
    {
        var uniqueCacheKeys = new HashSet<string>();

        var media = mediaAndFiles
            .GroupBy(x => x.MediaId)
            .Select(g =>
            {
                // side effect to simplify priming the cache
                uniqueCacheKeys.Add(CacheKeyBuilder.CanAccessAsset(userId, g.First().FilePath));

                return g;
            })
            .Select(g => new Media(
                g.Key,
                g.First().MediaSlug,
                g.First().CategoryId,
                g.First().MediaType,
                g.First().MediaIsFavorite,
                g.Select(x => new MediaFile(
                    x.FileId,
                    x.FileScale,
                    x.FileType,
                    assetPathBuilder.Build(baseUrl, x.FilePath)
                )).ToList()
            ))
            .ToList();

        foreach (var key in uniqueCacheKeys)
        {
            await cache.SetAsync(key, true);
        }

        return media;
    }
}
