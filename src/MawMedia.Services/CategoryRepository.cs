using Microsoft.Extensions.Logging;
using NodaTime;
using Npgsql;
using MawMedia.Models;
using MawMedia.Services.Models;

namespace MawMedia.Services;

public class CategoryRepository
    : BaseRepository, ICategoryRepository
{
    const int SEARCH_LIMIT_MAX = 250;

    readonly IAssetPathBuilder _assetPathBuilder;

    public CategoryRepository(
        ILogger<CategoryRepository> log,
        NpgsqlConnection conn,
        IAssetPathBuilder assetPathBuilder
    ) : base(log, conn)
    {
        ArgumentNullException.ThrowIfNull(assetPathBuilder);

        _assetPathBuilder = assetPathBuilder;
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

    public async Task<IEnumerable<Category>> GetCategories(Guid userId, string baseUrl, short? year = null) =>
        await InternalGetCategories(userId, baseUrl, year: year);

    public async Task<IEnumerable<Category>> GetCategoryUpdates(Guid userId, Instant date, string baseUrl) =>
        await InternalGetCategories(userId, baseUrl, modifiedAfter: date);

    public async Task<Category?> GetCategory(Guid userId, Guid categoryId, string baseUrl) =>
        (await InternalGetCategories(userId, baseUrl, categoryId))
            .SingleOrDefault();

    public async Task<IEnumerable<Gps>> GetCategoryMediaGps(Guid userId, Guid categoryId)
    {
        return await Query<Gps>(
            "SELECT * FROM media.get_media_gps(@userId, NULL, @categoryId);",
            new
            {
                userId,
                categoryId
            }
        );
    }

    public async Task<bool> SetIsFavorite(Guid userId, Guid categoryId, bool isFavorite)
    {
        var result = await ExecuteTransaction<int>(
            "SELECT * FROM media.favorite_category(@userId, @categoryId, @isFavorite);",
            new
            {
                userId,
                categoryId,
                isFavorite
            }
        );

        if (result == 0)
        {
            return true;
        }

        _log.LogWarning("Unable to set favorite category - user {USER} does not have access to category {CATEGORY} or category does not exist!", userId, categoryId);

        return false;
    }

    public async Task<bool> SetTeaserMedia(Guid userId, Guid categoryId, Guid mediaId)
    {
        var result = await ExecuteTransaction<int>(
            "SELECT * FROM media.set_category_teaser(@userId, @categoryId, @mediaId);",
            new
            {
                userId,
                categoryId,
                mediaId
            }
        );

        if (result == 0)
        {
            return true;
        }

        _log.LogWarning("Unable to set category teaser - user {USER} does not have access to category {CATEGORY} or media {MEDIA} - or category/media does not exist!", userId, categoryId, mediaId);

        return false;
    }

    public async Task<IEnumerable<Media>> GetCategoryMedia(Guid userId, Guid categoryId) =>
        await InternalGetCategoryMedia(userId, categoryId);

    public async Task<SearchResult<Category>> Search(
        Guid userId,
        string baseUrl,
        string searchTerm,
        int offset,
        int limit
    )
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be greater than or equal to 0.");
        }

        if (limit < 1 || limit > SEARCH_LIMIT_MAX)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, $"Limit must be between 1 and {SEARCH_LIMIT_MAX}.");
        }

        var results = await Query<CategoryAndTeaser>(
            "SELECT * FROM media.search_categories(@userId, @searchTerm, @offset, @limit);",
            new
            {
                userId,
                searchTerm,
                offset,
                limit = limit + 1
            }
        );

        return new SearchResult<Category>(
            ConvertToCategories(results, baseUrl),
            results.Count() > limit
        );
    }

    async Task<IEnumerable<Category>> InternalGetCategories(
        Guid userId,
        string baseUrl,
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

        return ConvertToCategories(results, baseUrl);
    }

    IEnumerable<Category> ConvertToCategories(IEnumerable<CategoryAndTeaser> results, string baseUrl) =>
        results
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
                    g.First().MediaIsFavorite,
                    g.Select(x => new MediaFile(
                        x.FileId,
                        x.FileScale,
                        x.FileType,
                        _assetPathBuilder.Build(baseUrl, x.FilePath)
                    )).ToList()
                )
            ))
            .ToList();

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

        return AssembleMedia(results);
    }
}
