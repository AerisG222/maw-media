using MawMedia.Models;
using MawMedia.Services.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using NodaTime;
using Npgsql;

namespace MawMedia.Services;

public class CategoryRepository
    : BaseRepository, ICategoryRepository
{
    const int SEARCH_LIMIT_MAX = 250;

    readonly HybridCache _cache;
    readonly IAssetPathBuilder _assetPathBuilder;

    public CategoryRepository(
        ILogger<CategoryRepository> log,
        NpgsqlConnection conn,
        HybridCache cache,
        IAssetPathBuilder assetPathBuilder
    ) : base(log, conn)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(assetPathBuilder);

        _cache = cache;
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
        var recs = await Query<GpsRecord>(
            "SELECT * FROM media.get_media_gps(@userId, NULL, @categoryId);",
            new
            {
                userId,
                categoryId
            }
        );

        return recs == null
            ? []
            : recs.Select(r => r.ToGps());
    }

    public async Task<bool> SetIsFavorite(Guid userId, Guid categoryId, bool isFavorite)
    {
        var result = await ExecuteScalarInTransaction<int>(
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
        var result = await ExecuteScalarInTransaction<int>(
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

    public async Task<IEnumerable<Media>> GetCategoryMedia(Guid userId, string baseUrl, Guid categoryId) =>
        await InternalGetCategoryMedia(userId, baseUrl, categoryId);

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
            "SELECT * FROM media.search_categories(@userId, @searchTerm, @offset, @limit, @excludeSrcFiles);",
            new
            {
                userId,
                searchTerm,
                offset,
                limit = limit + 1,
                excludeSrcFiles = true
            }
        );

        var categories = await ConvertToCategories(userId, results, baseUrl);
        var hasMore = categories.Count() > limit;

        return new SearchResult<Category>(
            categories,
            hasMore,
            hasMore ? offset + limit + 1 : 0
        );
    }

    public async Task<IEnumerable<Guid>> GetCategoriesWithoutGps(Guid userId, short? year) =>
        await Query<Guid>(
            "SELECT * FROM media.get_categories_without_gps(@userId, @year)",
            new
            {
                userId,
                year
            }
        );

    async Task<IEnumerable<Category>> InternalGetCategories(
        Guid userId,
        string baseUrl,
        Guid? categoryId = null,
        short? year = null,
        Instant? modifiedAfter = null
    )
    {
        var results = await Query<CategoryAndTeaser>(
            "SELECT * FROM media.get_categories(@userId, @categoryId, @year, @modifiedAfter, @excludeSrcFiles);",
            new
            {
                userId,
                categoryId,
                year,
                modifiedAfter,
                excludeSrcFiles = true
            }
        );

        return await ConvertToCategories(userId, results, baseUrl);
    }

    async Task<IEnumerable<Category>> ConvertToCategories(
        Guid userId,
        IEnumerable<CategoryAndTeaser> results,
        string baseUrl
    )
    {
        var uniqueCacheKeys = new HashSet<string>();

        var cats = results
            .GroupBy(x => x.Id)
            .Select(g =>
            {
                // side effect to simplify priming the cache
                uniqueCacheKeys.Add(CacheKeyBuilder.CanAccessAsset(userId, g.First().FilePath));

                return g;
            })
            .Select(g => new Category(
                g.Key,
                g.First().Year,
                g.First().Slug,
                g.First().Name,
                g.First().EffectiveDate,
                g.First().Modified,
                g.First().IsFavorite,
                new Media(
                    g.First().MediaId,
                    g.First().MediaSlug,
                    g.Key,
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

        foreach (var key in uniqueCacheKeys)
        {
            await _cache.SetAsync(key, true);
        }

        return cats;
    }

    async Task<IEnumerable<Media>> InternalGetCategoryMedia(
        Guid userId,
        string baseUrl,
        Guid categoryId
    )
    {
        var results = await Query<MediaAndFile>(
            "SELECT * FROM media.get_category_media(@userId, @categoryId, @excludeSrcFiles);",
            new
            {
                userId,
                categoryId,
                excludeSrcFiles = true
            }
        );

        return await AssembleMedia(userId, results, baseUrl, _assetPathBuilder, _cache);
    }
}
