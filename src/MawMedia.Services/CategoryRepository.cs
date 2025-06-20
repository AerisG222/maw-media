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

    public async Task<IEnumerable<Category>> GetCategories(Guid userId) => await InternalGetCategories(userId);
    public async Task<IEnumerable<Category>> GetCategoryUpdates(Guid userId, Instant date) => await InternalGetCategories(userId, modifiedAfter: date);
    public async Task<Category?> GetCategory(Guid userId, Guid categoryId) =>
        (await InternalGetCategories(userId, categoryId))
            .SingleOrDefault();

    public async Task<IEnumerable<Media>> GetCategoryMedia(Guid userId, Guid categoryId) => await InternalGetCategoryMedia(userId, categoryId);

    async Task<IEnumerable<Category>> InternalGetCategories(
        Guid userId,
        Guid? categoryId = null,
        short? year = null,
        Instant? modifiedAfter = null
    )
    {
        return await Query<Category>(
            "SELECT * FROM media.get_categories(@userId, @categoryId, @year, @modifiedAfter);",
            new
            {
                userId,
                categoryId,
                year,
                modifiedAfter
            }
        );
    }

    async Task<IEnumerable<Media>> InternalGetCategoryMedia(
        Guid userId,
        Guid categoryId
    )
    {
        var mediaList = new List<Media>();

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
