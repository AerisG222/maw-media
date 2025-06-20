using Microsoft.Extensions.Logging;
using NodaTime;
using Npgsql;
using MawMedia.Models;
using MawMedia.Services.Models;

namespace MawMedia.Services;

public class CategoryRepository
    : BaseRepository, ICategoryRepository
{
    public CategoryRepository(
        ILogger<CategoryRepository> log,
        NpgsqlConnection conn
    ) : base(log, conn)
    {

    }

    public async Task<IEnumerable<short>> GetCategoryYears(Guid userId)
    {
        return await Query<short>(
            "SELECT * FROM media.get_category_years(@userId);",
            new
            {
                userId
            }
        );
    }

    public async Task<IEnumerable<Category>> GetCategories(Guid userId, short? year = null) =>
        await InternalGetCategories(userId, year: year);

    public async Task<IEnumerable<Category>> GetCategoryUpdates(Guid userId, Instant date) =>
        await InternalGetCategories(userId, modifiedAfter: date);

    public async Task<Category?> GetCategory(Guid userId, Guid categoryId) =>
        (await InternalGetCategories(userId, categoryId))
            .SingleOrDefault();

    public async Task<IEnumerable<Media>> GetCategoryMedia(Guid userId, Guid categoryId) =>
        await InternalGetCategoryMedia(userId, categoryId);

    async Task<IEnumerable<Category>> InternalGetCategories(
        Guid userId,
        Guid? categoryId = null,
        short? year = null,
        Instant? modifiedAfter = null
    )
    {
        var results = await Query<CategoryAndTeaser>(
            "SELECT * FROM media.get_categories(@userId, @categoryId, @year, @modifiedAfter);",
            new
            {
                userId,
                categoryId,
                year,
                modifiedAfter
            }
        );

        return results
            .GroupBy(x => x.Id)
            .Select(g => new Category(
                g.Key,
                g.First().Name,
                g.First().EffectiveDate,
                g.First().Modified,
                g.First().IsFavorite,
                new Media(
                    g.First().MediaId,
                    g.First().MediaType,
                    g.Select(x => new MediaFile(
                        x.FileScale,
                        x.FileType,
                        x.FilePath
                    )).ToList()
                )
            ))
            .ToList();
    }

    async Task<IEnumerable<Media>> InternalGetCategoryMedia(
        Guid userId,
        Guid categoryId
    )
    {
        var results = await Query<MediaAndFile>(
            "SELECT * FROM media.get_category_media(@userId, @categoryId);",
            new
            {
                userId,
                categoryId
            }
        );

        return results
            .GroupBy(x => x.Id)
            .Select(g => new Media(
                g.Key,
                g.First().Type,
                g.Select(x => new MediaFile(
                    x.FileScale,
                    x.FileType,
                    x.FilePath
                )).ToList()
            ))
            .ToList();
    }
}
