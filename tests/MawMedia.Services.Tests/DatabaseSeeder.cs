using Npgsql;

namespace MawMedia.Services.Tests;

public class DatabaseSeeder
{
    readonly NpgsqlDataSource _dataSource;

    public DatabaseSeeder(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        _dataSource = dataSource;
    }

    public async Task PopuplateDatabase()
    {
        NpgsqlConnection? conn = null;
        NpgsqlTransaction? tran = null;

        try
        {
            conn = _dataSource.CreateConnection();
            await conn.OpenAsync();
            tran = await conn.BeginTransactionAsync();

            await PopulateData(conn);

            await tran.CommitAsync();
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

            if (conn != null)
            {
                await conn.CloseAsync();
                await conn.DisposeAsync();
            }
        }
    }

    Task PopulateData(NpgsqlConnection conn)
    {
        return Task.CompletedTask;
    }
}
