using System.Text.Json;
using Dapper;
using Npgsql;
using NpgsqlTypes;

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

        await PopulateLocations(conn);
        await PopulateCategories(conn);
        await PopulateCategoryRoles(conn);
        await PopulateMedia(conn);
        await PopulateCategoryMedia(conn);
        await PopulateFiles(conn);
        await PopulateComments(conn);
        await PopulateFavorites(conn);
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

    async Task PopulateCategories(NpgsqlConnection conn)
    {
        List<object> categories = [
            Constants.CATEGORY_NATURE,
            Constants.CATEGORY_TRAVEL
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.category (id, name, effective_date, created, created_by, modified, modified_by)
            VALUES
            (@id, @name, @effectiveDate, @created, @createdBy, @modified, @modifiedBy);
            """,
            categories
        );
    }

    async Task PopulateCategoryRoles(NpgsqlConnection conn)
    {
        List<object> categoryRoles = [
            new {
                category_id = Constants.CATEGORY_NATURE.Id,
                role_id = Constants.ROLE_ADMIN,
                created = DateTime.UtcNow,
                created_by = Constants.USER_ADMIN
            }
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.category_role (category_id, role_id, created, created_by)
            VALUES
            (@category_id, @role_id, @created, @created_by);
            """,
            categoryRoles
        );
    }

    async Task PopulateCategoryFavorites(NpgsqlConnection conn)
    {
        List<object> favorites = [
            new {
                created_by = Constants.USER_JOHNDOE,
                category_id = Constants.CATEGORY_NATURE,
                created = DateTime.UtcNow
            }
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.category_favorite (created_by, category_id, created)
            VALUES
            (@created_by, @category_id, @created);
            """,
            favorites
        );
    }

    async Task PopulateMedia(NpgsqlConnection conn)
    {
        List<dynamic> media = [
            new {
                id = Constants.MEDIA_NATURE_1,
                type_id = Constants.TYPE_PHOTO,
                location_id = Constants.LOCATION_NY,
                location_override_id = DBNull.Value,
                created = DateTime.UtcNow,
                created_by = Constants.USER_ADMIN,
                modified = DateTime.UtcNow,
                modified_by = Constants.USER_ADMIN,
                metadata = JsonDocument.Parse("{}")
            }
        ];

        foreach (var m in media)
        {
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO media.media (id, type_id, location_id, location_override_id, created, created_by, modified, modified_by, metadata)
                VALUES
                ($1, $2, $3, $4, $5, $6, $7, $8, $9);
                """,
                conn)
            {
                Parameters = {
                    new() { Value = m.id },
                    new() { Value = m.type_id },
                    new() { Value = m.location_id },
                    new() { Value = m.location_override_id, NpgsqlDbType = NpgsqlDbType.Uuid },
                    new() { Value = m.created },
                    new() { Value = m.created_by },
                    new() { Value = m.modified },
                    new() { Value = m.modified_by },
                    new() { Value = m.metadata }
                }
            };

            await cmd.ExecuteNonQueryAsync();
        }
    }

    async Task PopulateCategoryMedia(NpgsqlConnection conn)
    {
        List<object> categoryMedia = [
            new {
                category_id = Constants.CATEGORY_NATURE.Id,
                media_id = Constants.MEDIA_NATURE_1,
                is_teaser = true,
                created = DateTime.UtcNow,
                created_by = Constants.USER_ADMIN,
                modified = DateTime.UtcNow,
                modified_by = Constants.USER_ADMIN
            }
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.category_media (category_id, media_id, is_teaser, created, created_by, modified, modified_by)
            VALUES
            (@category_id, @media_id, @is_teaser, @created, @created_by, @modified, @modified_by);
            """,
            categoryMedia
        );
    }

    async Task PopulateFiles(NpgsqlConnection conn)
    {
        List<object> files = [
            new {
                media_id = Constants.MEDIA_NATURE_1,
                type_id = Constants.TYPE_PHOTO,
                scale_id = Constants.SCALE_FULL_HD,
                width = 1920,
                height = 1080,
                bytes = 123456L,
                path = "/media/nature1.jpg"
            }
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.file (media_id, type_id, scale_id, width, height, bytes, path)
            VALUES
            (@media_id, @type_id, @scale_id, @width, @height, @bytes, @path);
            """,
            files
        );
    }

    async Task PopulateComments(NpgsqlConnection conn)
    {
        List<object> comments = [
            new {
                id = Guid.NewGuid(),
                media_id = Constants.MEDIA_NATURE_1,
                created = DateTime.UtcNow,
                created_by = Constants.USER_JOHNDOE,
                modified = DateTime.UtcNow,
                body = "Great photo!"
            }
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.comment (id, media_id, created, created_by, modified, body)
            VALUES
            (@id, @media_id, @created, @created_by, @modified, @body);
            """,
            comments
        );
    }

    async Task PopulateFavorites(NpgsqlConnection conn)
    {
        List<object> favorites = [
            new {
                media_id = Constants.MEDIA_NATURE_1,
                created_by = Constants.USER_JOHNDOE,
                created = DateTime.UtcNow
            }
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.favorite (media_id, created_by, created)
            VALUES
            (@media_id, @created_by, @created);
            """,
            favorites
        );
    }

    async Task PopulateLocations(NpgsqlConnection conn)
    {
        List<object> locations = [
            new {
                id = Constants.LOCATION_NY,
                latitude = 40.712776m,
                longitude = -74.005974m,
                lookup_date = DateTime.UtcNow,
                formatted_address = "New York, NY, USA",
                administrative_area_level_1 = "NY",
                administrative_area_level_2 = "New York County",
                administrative_area_level_3 = (string?)null,
                country = "USA",
                locality = "New York",
                neighborhood = "Manhattan",
                sub_locality_level_1 = (string?)null,
                sub_locality_level_2 = (string?)null,
                postal_code = "10007",
                postal_code_suffix = (string?)null,
                premise = (string?)null,
                route = "Broadway",
                street_number = "1",
                sub_premise = (string?)null
            }
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.location (
                id, latitude, longitude, lookup_date, formatted_address,
                administrative_area_level_1, administrative_area_level_2, administrative_area_level_3,
                country, locality, neighborhood, sub_locality_level_1, sub_locality_level_2,
                postal_code, postal_code_suffix, premise, route, street_number, sub_premise
            )
            VALUES
            (
                @id, @latitude, @longitude, @lookup_date, @formatted_address,
                @administrative_area_level_1, @administrative_area_level_2, @administrative_area_level_3,
                @country, @locality, @neighborhood, @sub_locality_level_1, @sub_locality_level_2,
                @postal_code, @postal_code_suffix, @premise, @route, @street_number, @sub_premise
            );
            """,
            locations
        );
    }
}
