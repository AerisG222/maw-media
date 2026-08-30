using MawMedia.Models;
using MawMedia.Services.Abstractions;
using MawMedia.Services.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MawMedia.Services;

public class PlaceRepository
    : BaseRepository, IPlaceRepository
{
    // a page bigger than this is almost certainly a client bug rather than a
    // genuine request, matching the ceiling the person and category views use
    public const int PLACE_MEDIA_LIMIT_MAX = 250;

    readonly IAssetPathBuilder _assetPathBuilder;
    readonly HybridCache _cache;

    public PlaceRepository(
        ILogger<PlaceRepository> log,
        NpgsqlConnection conn,
        IAssetPathBuilder assetPathBuilder,
        HybridCache cache
    ) : base(log, conn)
    {
        ArgumentNullException.ThrowIfNull(assetPathBuilder);
        ArgumentNullException.ThrowIfNull(cache);

        _assetPathBuilder = assetPathBuilder;
        _cache = cache;
    }

    public async Task<IEnumerable<Place>> GetPlaces(
        Guid userId,
        Guid? parentId = null,
        string? kind = null,
        CancellationToken token = default
    ) => await InternalGetPlaces(userId, parentId, kind, null, token);

    public async Task<Place?> GetPlace(
        Guid userId,
        Guid placeId,
        CancellationToken token = default
    ) => (await InternalGetPlaces(userId, null, null, placeId, token)).SingleOrDefault();

    async Task<IEnumerable<Place>> InternalGetPlaces(
        Guid userId,
        Guid? parentId,
        string? kind,
        Guid? placeId,
        CancellationToken token
    )
    {
        // media.get_places takes placeId as the same single-row narrowing trick
        // media.get_persons uses, so the list and the fetch are one query with one
        // access rule rather than two that could drift apart
        var results = await Query<PlaceRow>(
            "SELECT * FROM media.get_places(@userId, @parentId, @kind, @placeId);",
            new
            {
                userId,
                parentId,
                kind,
                placeId
            },
            token
        );

        return results
            .Select(r => new Place(
                r.Id,
                r.ParentId,
                r.Kind,
                r.Name,
                r.Slug,
                r.MediaCount
            ))
            .ToList();
    }

    public async Task<IEnumerable<PlaceAncestor>> GetPlaceAncestors(
        Guid userId,
        Guid placeId,
        CancellationToken token = default
    )
    {
        var results = await Query<PlaceAncestorRow>(
            "SELECT * FROM media.get_place_ancestors(@placeId);",
            new
            {
                placeId
            },
            token
        );

        return results
            .Select(r => new PlaceAncestor(
                r.AncestorId,
                r.AncestorParentId,
                r.AncestorKind,
                r.AncestorName,
                r.AncestorSlug,
                r.AncestorDepth
            ))
            .ToList();
    }

    public async Task<SearchResult<Media>> GetPlaceMedia(
        Guid userId,
        string baseUrl,
        Guid placeId,
        int offset,
        int limit,
        bool favoritesOnly = false,
        long? seed = null,
        CancellationToken token = default
    )
    {
        ValidatePaging(offset, limit);

        // one extra row tells us whether another page exists without a second
        // count query.  the function pages over media, so the extra row is an
        // extra media item, not an extra file.
        var results = await Query<MediaAndFile>(
            "SELECT * FROM media.get_place_media(@userId, @placeId, @offset, @limit, @excludeSrcFiles, @favoritesOnly, @seed);",
            new
            {
                userId,
                placeId,
                offset,
                limit = limit + 1,
                excludeSrcFiles = true,
                favoritesOnly,
                seed
            },
            token
        );

        var media = (await AssembleMedia(userId, results, baseUrl, _assetPathBuilder, _cache, token)).ToList();

        return Page(media, offset, limit);
    }

    public async Task<SearchResult<Category>> GetPlaceCategories(
        Guid userId,
        string baseUrl,
        Guid placeId,
        int offset,
        int limit,
        bool favoritesOnly = false,
        CancellationToken token = default
    )
    {
        ValidatePaging(offset, limit);

        // the same extra-row trick the media view uses.  the function pages over
        // categories, so the extra row is an extra category, not an extra file.
        var results = await Query<CategoryAndTeaser>(
            "SELECT * FROM media.get_place_categories(@userId, @placeId, @offset, @limit, @excludeSrcFiles, @favoritesOnly);",
            new
            {
                userId,
                placeId,
                offset,
                limit = limit + 1,
                excludeSrcFiles = true,
                favoritesOnly
            },
            token
        );

        var categories = (await AssembleCategories(userId, results, baseUrl, _assetPathBuilder, _cache, token)).ToList();

        return Page(categories, offset, limit);
    }

    static void ValidatePaging(int offset, int limit)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be greater than or equal to 0.");
        }

        if (limit < 1 || limit > PLACE_MEDIA_LIMIT_MAX)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, $"Limit must be between 1 and {PLACE_MEDIA_LIMIT_MAX}.");
        }
    }

    // the extra row is trimmed here rather than at each call site, so the media and
    // category views cannot disagree about what NextOffset means on the last page
    static SearchResult<T> Page<T>(List<T> items, int offset, int limit)
    {
        var hasMore = items.Count > limit;

        return new SearchResult<T>(
            hasMore ? items.Take(limit) : items,
            hasMore,
            hasMore ? offset + limit : 0
        );
    }
}
