using Microsoft.Extensions.Logging;
using Dapper;
using Npgsql;
using MawMedia.Models;
using MawMedia.Services.Models;

namespace MawMedia.Services;

public class BaseRepository
{
    readonly ILogger _log;
    readonly NpgsqlConnection _conn;

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

    protected async Task ExecuteTransaction(
        string statement,
        object? param = null
    )
    {
        await RunTransaction(
            conn =>
                conn.ExecuteAsync(
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

    protected async Task<T> RunTransaction<T>(Func<NpgsqlConnection, Task<T>> command, bool runInTransaction = false)
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

    internal IEnumerable<Media> AssembleMedia(IEnumerable<MediaAndFile> mediaAndFiles) =>
        mediaAndFiles
            .GroupBy(x => x.MediaId)
            .Select(g => new Media(
                g.Key,
                g.First().MediaType,
                g.First().MediaIsFavorite,
                g.Select(x => new MediaFile(
                    x.FileScale,
                    x.FileType,
                    x.FilePath
                )).ToList()
            ))
            .ToList();
}
