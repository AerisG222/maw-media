using Dapper;
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
    readonly IPlaceCoverStore _coverStore;

    public PlaceRepository(
        ILogger<PlaceRepository> log,
        NpgsqlConnection conn,
        IAssetPathBuilder assetPathBuilder,
        HybridCache cache,
        IPlaceCoverStore coverStore
    ) : base(log, conn)
    {
        ArgumentNullException.ThrowIfNull(assetPathBuilder);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(coverStore);

        _assetPathBuilder = assetPathBuilder;
        _cache = cache;
        _coverStore = coverStore;
    }

    public async Task<IEnumerable<Place>> GetPlaces(
        Guid userId,
        string baseUrl,
        Guid? parentId = null,
        string? kind = null,
        string? search = null,
        CancellationToken token = default
    ) => await InternalGetPlaces(userId, baseUrl, parentId, kind, null, search, token);

    public async Task<Place?> GetPlace(
        Guid userId,
        string baseUrl,
        Guid placeId,
        CancellationToken token = default
    ) => (await InternalGetPlaces(userId, baseUrl, null, null, placeId, null, token)).SingleOrDefault();

    async Task<IEnumerable<Place>> InternalGetPlaces(
        Guid userId,
        string baseUrl,
        Guid? parentId,
        string? kind,
        Guid? placeId,
        string? search,
        CancellationToken token
    )
    {
        // media.get_places takes placeId as the same single-row narrowing trick
        // media.get_persons uses, so the list and the fetch are one query with one
        // access rule rather than two that could drift apart
        var results = await Query<PlaceRow>(
            "SELECT * FROM media.get_places(@userId, @parentId, @kind, @placeId, @search);",
            new
            {
                userId,
                parentId,
                kind,
                placeId,
                search
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
                r.MediaCount,
                // null for a country, which has none - flattened here so clients
                // never have to distinguish "no ancestors" from "not supplied"
                r.AncestorNames ?? [],
                // absolute, so clients do not assemble it.  the file name is the
                // place id; cover_created only says whether there is one and which
                // version, which becomes the url's ?v=.
                r.CoverCreated == null
                    ? null
                    : _assetPathBuilder.Build(baseUrl, string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        Constants.PlaceCoverUrlFormat,
                        r.Id,
                        r.CoverCreated.Value.ToUnixTimeTicks())),
                r.CoverMediaId
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

    public async Task<PlaceCoverOutcome> SetPlaceCover(
        Guid userId,
        Guid placeId,
        Guid mediaId,
        CancellationToken token = default
    )
    {
        // one fixed scale rather than the largest that fits: 'qvg-fill' is cropped
        // to fill, so every tile is the same shape whatever the source photograph's
        // aspect.  never 'src' - the function refuses it by name, because the
        // originals carry gps and camera identity in exif.
        var chosen = await QuerySingle<CoverCandidate>(
            "SELECT * FROM media.get_place_cover_candidate(@userId, @mediaId, @scale);",
            new { userId, mediaId, scale = Constants.PlaceCoverScale },
            token
        );

        if (chosen == null)
        {
            // either the media has no rendition at that scale, or the caller cannot
            // see it.  the two are one answer on purpose - see PlaceCoverOutcome.
            return PlaceCoverOutcome.NoPublishableRendition;
        }

        // published before the row is written, so a row claiming a cover never
        // precedes the file - a tile has no way to recover from a broken image.
        // replacing overwrites {placeId}.avif, so there is no displaced name to
        // track and nothing to clean up on the happy path.
        await _coverStore.Publish(placeId, chosen.FilePath, token);

        var result = await QuerySingle<CoverChange>(
            "SELECT * FROM media.set_place_cover(@userId, @placeId, @mediaId, @fileId);",
            new { userId, placeId, mediaId, fileId = chosen.FileId },
            token
        );

        if (result?.Result != 0)
        {
            // the database refused, so nothing references the file just written.
            // take it back out - though note this also removes a cover the place
            // may already have had, which is the one cost of overwriting in place.
            _coverStore.Delete(placeId);

            return result?.Result switch
            {
                1 => PlaceCoverOutcome.NotAdmin,
                2 => PlaceCoverOutcome.PlaceNotFound,
                _ => PlaceCoverOutcome.MediaNotAtPlace
            };
        }

        return PlaceCoverOutcome.Ok;
    }

    public async Task<PlaceCoverOutcome> ClearPlaceCover(
        Guid userId,
        Guid placeId,
        CancellationToken token = default
    )
    {
        // the row is cleared first and the file deleted after, the reverse of
        // setting one: a tile that has stopped pointing at an image cannot break
        // when the bytes go
        var result = await QuerySingle<CoverChange>(
            "SELECT * FROM media.clear_place_cover(@userId, @placeId);",
            new { userId, placeId },
            token
        );

        if (result?.Result == 1)
        {
            return PlaceCoverOutcome.NotAdmin;
        }

        // a file left behind is a leftover byte, not a disclosure of anything new -
        // nothing points at it any more - so failing to remove it must not fail the
        // request
        try
        {
            _coverStore.Delete(placeId);
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Could not remove the cover file for place {PLACE}", placeId);
        }

        return PlaceCoverOutcome.Ok;
    }

    public async Task<PlaceAdminOutcome> MergePlaces(
        Guid userId,
        Guid winnerId,
        Guid loserId,
        CancellationToken token = default
    )
    {
        var result = await QuerySingle<MergeResult>(
            "SELECT * FROM media.merge_places(@userId, @winnerId, @loserId);",
            new { userId, winnerId, loserId },
            token
        );

        if (result?.Result != 0)
        {
            return result?.Result switch
            {
                1 => PlaceAdminOutcome.NotAdmin,
                4 => PlaceAdminOutcome.SamePlace,
                5 => PlaceAdminOutcome.KindMismatch,
                6 => PlaceAdminOutcome.InvalidParent,
                _ => PlaceAdminOutcome.NotFound
            };
        }

        _log.LogInformation(
            "Merged place {LOSER} into {WINNER}: {LOCATIONS} locations and {CHILDREN} children moved",
            loserId, winnerId, result.MovedLocations, result.MovedChildren);

        // the row is gone but its published cover is not, and nothing points at it
        // any more.  left behind it would still be served to anyone holding the
        // dead id, so it goes - but failing to remove it must not fail a merge that
        // has already committed.
        if (result.HadCover)
        {
            try
            {
                _coverStore.Delete(loserId);
            }
            catch (IOException ex)
            {
                _log.LogWarning(ex, "Could not remove the cover of merged place {PLACE}", loserId);
            }
        }

        return PlaceAdminOutcome.Ok;
    }

    public async Task<PlaceAdminOutcome> SetPlaceParent(
        Guid userId,
        Guid placeId,
        Guid? parentId,
        CancellationToken token = default
    )
    {
        var result = await QuerySingle<int?>(
            "SELECT * FROM media.set_place_parent(@userId, @placeId, @parentId);",
            new { userId, placeId, parentId },
            token
        );

        return result switch
        {
            0 => PlaceAdminOutcome.Ok,
            1 => PlaceAdminOutcome.NotAdmin,
            4 or 5 => PlaceAdminOutcome.InvalidParent,
            6 => PlaceAdminOutcome.NotARootKind,
            _ => PlaceAdminOutcome.NotFound
        };
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
