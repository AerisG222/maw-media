using Dapper;
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

    async Task PopulateData(NpgsqlConnection conn)
    {
        await PopulateUsers(conn);
        await PopulateRoles(conn);
        await PopulateUserRoles(conn);
    }

    async Task PopulateRoles(NpgsqlConnection conn)
    {
        List<object> roles = [
            new {
                id = Constants.ROLE_ADMIN,
                name = "admin",
                created = DateTime.Now,
                created_by = Constants.USER_ADMIN
            },
            new {
                id = Constants.ROLE_FRIEND,
                name = "friend",
                created = DateTime.Now,
                created_by = Constants.USER_ADMIN
            },
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.role (id, name, created, created_by)
            VALUES
            (@id, @name, @created, @created_by);
            """,
            roles
        );
    }

    async Task PopulateUsers(NpgsqlConnection conn)
    {
        List<object> users = [
            new {
                id = Constants.USER_ADMIN,
                created = DateTime.Now,
                modified = DateTime.Now,
                name = "Admin",
                email = "admin@example.com",
                email_verified = true,
                given_name = "AdminGiven",
                surname = "AdminSurname"
            },
            new {
                id = Constants.USER_JOHNDOE,
                created = DateTime.Now,
                modified = DateTime.Now,
                name = "jdoe",
                email = "jdoe@example.com",
                email_verified = true,
                given_name = "John",
                surname = "Doe"
            }
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.user (id, created, modified, name, email, email_verified, given_name, surname)
            VALUES
            (@id, @created, @modified, @name, @email, @email_verified, @given_name, @surname);
            """,
            users
        );
    }

    async Task PopulateUserRoles(NpgsqlConnection conn)
    {
        var userRoles = Constants.UserRoles
            .Select(role => new
            {
                user_id = role.user_id,
                role_id = role.role_id,
                created = DateTime.Now,
                created_by = Constants.USER_ADMIN
            });

        await conn.ExecuteAsync(
            """
            INSERT INTO media.user_role (user_id, role_id, created, created_by)
            VALUES
            (@user_id, @role_id, @created, @created_by)
            """,
            userRoles
        );
    }
}
