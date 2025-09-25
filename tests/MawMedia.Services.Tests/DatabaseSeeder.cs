using System.Text.Json;
using Dapper;
using MawMedia.Services.Tests.Models;
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
        await PopulateExternalIdentities(conn);
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

        await RefreshMaterializedViews(conn);
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

    async Task PopulateExternalIdentities(NpgsqlConnection conn)
    {
        List<object> externalIds = [
            new {
                external_id = Constants.EXTERNAL_ID_NOUSER,
                user_id = (Guid?) null,
                created = DateTime.Now,
                modified = DateTime.Now,
                name = "No User",
                email = "nouser@example.com",
                email_verified = true,
            },
            new {
                external_id = Constants.EXTERNAL_ID_USERADMIN,
                user_id = (Guid?)Constants.USER_ADMIN,
                created = DateTime.Now,
                modified = DateTime.Now,
                name = "Admin User",
                email = "adminuser@example.com",
                email_verified = true,
            },
            new {
                external_id = Constants.EXTERNAL_ID_JOHNDOE,
                user_id = (Guid?)Constants.USER_JOHNDOE,
                created = DateTime.Now,
                modified = DateTime.Now,
                name = "John Doe",
                email = "jdoe@example.com",
                email_verified = true,
            }
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.external_identity (external_id, user_id, created, modified, name, email, email_verified)
            VALUES
            (@external_id, @user_id, @created, @modified, @name, @email, @email_verified);
            """,
            externalIds
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
            Constants.CATEGORY_TRAVEL,
            Constants.CATEGORY_FOOD
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
            },
            new {
                category_id = Constants.CATEGORY_TRAVEL.Id,
                role_id = Constants.ROLE_ADMIN,
                created = DateTime.UtcNow,
                created_by = Constants.USER_ADMIN
            },
            new {
                category_id = Constants.CATEGORY_TRAVEL.Id,
                role_id = Constants.ROLE_FRIEND,
                created = DateTime.UtcNow,
                created_by = Constants.USER_ADMIN
            },
            new {
                category_id = Constants.CATEGORY_FOOD.Id,
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
        List<DbMedia> media = [
            Constants.MEDIA_NATURE_1,
            Constants.MEDIA_NATURE_2,
            Constants.MEDIA_TRAVEL_1,
            Constants.MEDIA_FOOD_1,
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
                    new() { Value = m.Id },
                    new() { Value = m.TypeId },
                    new() { Value = m.LocationId },
                    new() { Value = (object?)m.LocationOverrideId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid },
                    new() { Value = m.Created },
                    new() { Value = m.CreatedBy },
                    new() { Value = m.Modified },
                    new() { Value = m.ModifiedBy },
                    new() { Value = m.Metadata }
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
                media_id = Constants.MEDIA_NATURE_1.Id,
                is_teaser = true,
                created = DateTime.UtcNow,
                created_by = Constants.USER_ADMIN,
                modified = DateTime.UtcNow,
                modified_by = Constants.USER_ADMIN
            },
            new {
                category_id = Constants.CATEGORY_NATURE.Id,
                media_id = Constants.MEDIA_NATURE_2.Id,
                is_teaser = false,
                created = DateTime.UtcNow,
                created_by = Constants.USER_ADMIN,
                modified = DateTime.UtcNow,
                modified_by = Constants.USER_ADMIN
            },
            new {
                category_id = Constants.CATEGORY_TRAVEL.Id,
                media_id = Constants.MEDIA_TRAVEL_1.Id,
                is_teaser = true,
                created = DateTime.UtcNow,
                created_by = Constants.USER_ADMIN,
                modified = DateTime.UtcNow,
                modified_by = Constants.USER_ADMIN
            },
            new {
                category_id = Constants.CATEGORY_FOOD.Id,
                media_id = Constants.MEDIA_FOOD_1.Id,
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
        List<DbFile> files = [
            Constants.FILE_NATURE_1,
            Constants.FILE_NATURE_2,
            Constants.FILE_TRAVEL_1
        ];

        await conn.ExecuteAsync(
            """
            INSERT INTO media.file (id, media_id, type_id, scale_id, width, height, bytes, path)
            VALUES
            (@id, @mediaId, @typeId, @scaleId, @width, @height, @bytes, @path);
            """,
            files
        );
    }

    async Task PopulateComments(NpgsqlConnection conn)
    {
        List<object> comments = [
            new {
                id = Guid.NewGuid(),
                media_id = Constants.MEDIA_NATURE_1.Id,
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
                media_id = Constants.MEDIA_NATURE_1.Id,
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
            Constants.LOCATION_NY
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
                @id, @latitude, @longitude, @lookupDate, @formattedAddress,
                @administrativeAreaLevel1, @administrativeAreaLevel2, @administrativeAreaLevel3,
                @country, @locality, @neighborhood, @subLocalityLevel1, @subLocalityLevel2,
                @postalCode, @postalCodeSuffix, @premise, @route, @streetNumber, @subPremise
            );
            """,
            locations
        );
    }

    async Task RefreshMaterializedViews(NpgsqlConnection conn)
    {
        await conn.ExecuteAsync("REFRESH MATERIALIZED VIEW media.category_search;");
    }
}
