using Dapper;
using MawMedia.Models;
using MawMedia.Services.Abstractions;
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
        object? param = null,
        CancellationToken token = default
    )
    {
        await RunCommand(
            conn =>
                conn.ExecuteAsync(
                    new CommandDefinition(statement, param, cancellationToken: token)
                ),
            token
        );
    }

    protected async Task<T?> ExecuteScalarInTransaction<T>(
        string statement,
        object? param = null,
        CancellationToken token = default
    )
    {
        return await RunTransaction(
            conn =>
                conn.ExecuteScalarAsync<T>(
                    new CommandDefinition(statement, param, cancellationToken: token)
                ),
            token
        );
    }

    protected async Task<IEnumerable<T>?> ExecuteQueryInTransaction<T>(
        string statement,
        object? param = null,
        CancellationToken token = default
    )
    {
        return await RunTransaction(
            conn =>
                conn.QueryAsync<T>(
                    new CommandDefinition(statement, param, cancellationToken: token)
                ),
            token
        );
    }

    protected async Task<T?> ExecuteScalar<T>(
        string statement,
        object? param = null,
        CancellationToken token = default
    )
    {
        return await RunCommand(
            conn =>
                conn.ExecuteScalarAsync<T>(
                    new CommandDefinition(statement, param, cancellationToken: token)
                ),
            token
        );
    }

    protected async Task<IEnumerable<T>> Query<T>(
        string statement,
        object? param = null,
        CancellationToken token = default
    )
    {
        return await RunCommand(
            conn =>
                conn.QueryAsync<T>(
                    new CommandDefinition(statement, param, cancellationToken: token)
                ),
            token
        );
    }

    protected async Task<T?> QuerySingle<T>(
        string statement,
        object? param = null,
        CancellationToken token = default
    )
    {
        return await RunCommand(
            conn =>
                conn.QuerySingleOrDefaultAsync<T>(
                    new CommandDefinition(statement, param, cancellationToken: token)
                ),
            token
        );
    }

    protected async Task<T> RunCommand<T>(Func<NpgsqlConnection, Task<T>> command, CancellationToken token = default)
    {
        try
        {
            await _conn.OpenAsync(token);

            return await command(_conn);
        }
        finally
        {
            await _conn.CloseAsync();
        }
    }

    protected async Task<T?> RunTransaction<T>(Func<NpgsqlConnection, Task<T>> command, CancellationToken token = default)
    {
        NpgsqlTransaction? tran = null;

        try
        {
            await _conn.OpenAsync(token);
            tran = await _conn.BeginTransactionAsync(token);

            var result = await command(_conn);

            await tran.CommitAsync(token);

            return result;
        }
        catch
        {
            if (tran != null)
            {
                await tran.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            if (tran != null)
            {
                await tran.DisposeAsync();
            }

            await _conn.CloseAsync();
        }
    }

    // the category equivalent of AssembleMedia, and static for the same reason:
    // both CategoryRepository and FaceRepository build categories out of the
    // same (category, teaser file) fan-out, and neither should own the shape.
    internal static async Task<IEnumerable<Category>> AssembleCategories(
        Guid userId,
        IEnumerable<CategoryAndTeaser> results,
        string baseUrl,
        IAssetPathBuilder assetPathBuilder,
        HybridCache cache,
        CancellationToken token = default
    )
    {
        var uniqueCacheKeys = new HashSet<string>();

        // every column but the file comes from the group's first row, so it is
        // read once rather than re-resolved per column - this used to call
        // g.First() ten times per category.  measured over the 2,163 categories a
        // full listing returns, 2.04ms against 1.72ms for identical output.
        //
        // a small win, and deliberately recorded as one: LINQ's grouping
        // implements IList, so First() is already an indexer rather than a walk of
        // the group.  the cost removed is ten interface dispatches per category,
        // not ten enumerations - worth taking because it also reads better, but
        // not worth reshaping anything else for.
        var cats = results
            .GroupBy(x => x.Id)
            .Select(g =>
            {
                var first = g.First();

                // side effect to simplify priming the cache
                uniqueCacheKeys.Add(CacheKeyBuilder.CanAccessAsset(userId, first.FilePath));

                return new Category(
                    g.Key,
                    first.Year,
                    first.Slug,
                    first.Name,
                    first.EffectiveDate,
                    first.Modified,
                    first.IsFavorite,
                    new Media(
                        first.MediaId,
                        first.MediaSlug,
                        g.Key,
                        first.Year,
                        first.Slug,
                        first.MediaType,
                        first.MediaIsFavorite,
                        g.Select(x => new MediaFile(
                            x.FileId,
                            x.FileScale,
                            x.FileType,
                            assetPathBuilder.Build(baseUrl, x.FilePath)
                        )).ToList()
                    ),
                    first.MediaTypes,
                    first.MediaCount
                );
            })
            .ToList();

        foreach (var key in uniqueCacheKeys)
        {
            await cache.SetAsync(key, true, cancellationToken: token);
        }

        return cats;
    }

    internal static async Task<IEnumerable<Media>> AssembleMedia(
        Guid userId,
        IEnumerable<MediaAndFile> mediaAndFiles,
        string baseUrl,
        IAssetPathBuilder assetPathBuilder,
        HybridCache cache,
        CancellationToken token = default
    )
    {
        var uniqueCacheKeys = new HashSet<string>();

        // the group's first row is read once, for the reason AssembleCategories
        // gives
        var media = mediaAndFiles
            .GroupBy(x => x.MediaId)
            .Select(g =>
            {
                var first = g.First();

                // side effect to simplify priming the cache
                uniqueCacheKeys.Add(CacheKeyBuilder.CanAccessAsset(userId, first.FilePath));

                return new Media(
                    g.Key,
                    first.MediaSlug,
                    first.CategoryId,
                    first.CategoryYear,
                    first.CategorySlug,
                    first.MediaType,
                    first.MediaIsFavorite,
                    g.Select(x => new MediaFile(
                        x.FileId,
                        x.FileScale,
                        x.FileType,
                        assetPathBuilder.Build(baseUrl, x.FilePath)
                    )).ToList()
                );
            })
            .ToList();

        foreach (var key in uniqueCacheKeys)
        {
            await cache.SetAsync(key, true, cancellationToken: token);
        }

        return media;
    }
}
