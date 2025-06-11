using Microsoft.Extensions.Logging;
using Dapper;
using Npgsql;

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
}
