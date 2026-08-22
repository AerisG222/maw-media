using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MawMedia.Models.FaceRecognition;
using MawMedia.Services.Abstractions;
using MawMedia.Services.Models;
using MawMedia.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Npgsql;

namespace MawMedia.Services;

public class FaceRepository
    : BaseRepository, IFaceRepository
{
    // the payload is consumed by the media.sync_* functions via
    // jsonb_to_recordset, whose column names are snake_case.  the http boundary
    // stays camelCase like every other endpoint - this policy applies only on
    // the way to postgres.
    static readonly JsonSerializerOptions PayloadOptions =
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        }.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    // a page bigger than this is almost certainly a client bug rather than a
    // genuine request - the picker shows a grid, and nobody scrolls 250 photos
    // in one go.  matches the ceiling category search uses.
    public const int PERSON_MEDIA_LIMIT_MAX = 250;

    readonly IAssetPathBuilder _assetPathBuilder;
    readonly HybridCache _cache;

    public FaceRepository(
        ILogger<FaceRepository> log,
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

    public async Task<IEnumerable<FaceSyncResult>> SyncPersonStatuses(
        Guid userId,
        IEnumerable<PersonStatusSync> statuses,
        CancellationToken token = default
    ) => await Sync("media.sync_person_statuses", userId, statuses, token);

    public async Task<IEnumerable<FaceSyncResult>> SyncPersons(
        Guid userId,
        IEnumerable<PersonSync> persons,
        CancellationToken token = default
    ) => await Sync("media.sync_persons", userId, persons, token);

    public async Task<IEnumerable<FaceSyncResult>> SyncFaces(
        Guid userId,
        IEnumerable<FaceSync> faces,
        CancellationToken token = default
    ) => await Sync("media.sync_faces", userId, faces, token);

    public async Task<IEnumerable<FaceSyncResult>> DeletePersons(
        Guid userId,
        IEnumerable<Guid> personIds,
        CancellationToken token = default
    ) => await Sync("media.delete_persons", userId, personIds, token);

    public async Task<IEnumerable<FaceSyncResult>> DeleteFaces(
        Guid userId,
        IEnumerable<Guid> faceIds,
        CancellationToken token = default
    ) => await Sync("media.delete_faces", userId, faceIds, token);

    public async Task<bool> FaceExists(
        Guid userId,
        Guid faceId,
        CancellationToken token = default
    ) => await ExecuteScalar<bool>(
        "SELECT * FROM media.get_face_exists(@userId, @faceId);",
        new
        {
            userId,
            faceId
        },
        token
    );

    public async Task<IEnumerable<Person>> GetPersons(
        Guid userId,
        string baseUrl,
        bool favoritesOnly = false,
        CancellationToken token = default
    ) => await InternalGetPersons(userId, baseUrl, favoritesOnly, null, token);

    public async Task<Person?> GetPerson(
        Guid userId,
        string baseUrl,
        Guid personId,
        CancellationToken token = default
    ) => (await InternalGetPersons(userId, baseUrl, false, personId, token))
        .SingleOrDefault();

    public async Task<bool> SetPersonIsFavorite(
        Guid userId,
        Guid personId,
        bool isFavorite,
        CancellationToken token = default
    )
    {
        var result = await ExecuteScalarInTransaction<int>(
            "SELECT * FROM media.favorite_person(@userId, @personId, @isFavorite);",
            new
            {
                userId,
                personId,
                isFavorite
            },
            token
        );

        if (result == 0)
        {
            return true;
        }

        _log.LogWarning("Unable to set favorite person - user {USER} does not have access to person {PERSON} or person does not exist!", userId, personId);

        return false;
    }

    async Task<IEnumerable<Person>> InternalGetPersons(
        Guid userId,
        string baseUrl,
        bool favoritesOnly,
        Guid? personId,
        CancellationToken token
    )
    {
        var rows = await Query<PersonRow>(
            "SELECT * FROM media.get_persons(@userId, @favoritesOnly, @personId);",
            new
            {
                userId,
                favoritesOnly,
                personId
            },
            token
        );

        // the picker renders one image per person, so these exact faces are about
        // to be requested as static assets - each of which authorizes through
        // CanViewFace.  priming here turns a few hundred round trips into none.
        // the rows came from media.user_face, so every id is one this caller may
        // already see.
        foreach (var faceId in rows.Where(r => r.PreferredFaceId != null).Select(r => r.PreferredFaceId!.Value))
        {
            await _cache.SetAsync(
                CacheKeyBuilder.CanViewFace(userId, faceId),
                true,
                cancellationToken: token
            );
        }

        return rows
            .Select(r => new Person(
                r.Id,
                r.Name,
                r.Slug,
                r.PreferredFaceId,
                FaceImageUrl(r.PreferredFaceId, baseUrl),
                r.MediaCount,
                r.IsFavorite
            ))
            .ToList();
    }

    public async Task<SearchResult<Media>> GetPersonMedia(
        Guid userId,
        string baseUrl,
        Guid personId,
        int offset,
        int limit,
        bool favoritesOnly = false,
        long? seed = null,
        CancellationToken token = default
    ) => await InternalGetPersonMedia(userId, baseUrl, personId, null, offset, limit, favoritesOnly, seed, token);

    public async Task<SearchResult<Media>> GetClanMedia(
        Guid userId,
        string baseUrl,
        Guid clanId,
        int offset,
        int limit,
        bool favoritesOnly = false,
        long? seed = null,
        CancellationToken token = default
    ) => await InternalGetPersonMedia(userId, baseUrl, null, clanId, offset, limit, favoritesOnly, seed, token);

    async Task<SearchResult<Media>> InternalGetPersonMedia(
        Guid userId,
        string baseUrl,
        Guid? personId,
        Guid? clanId,
        int offset,
        int limit,
        bool favoritesOnly,
        long? seed,
        CancellationToken token
    )
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be greater than or equal to 0.");
        }

        if (limit < 1 || limit > PERSON_MEDIA_LIMIT_MAX)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, $"Limit must be between 1 and {PERSON_MEDIA_LIMIT_MAX}.");
        }

        // one extra row tells us whether another page exists without a second
        // count query.  the function pages over media, so the extra row is an
        // extra media item, not an extra file.
        var results = await Query<MediaAndFile>(
            "SELECT * FROM media.get_person_media(@userId, @personId, @offset, @limit, @excludeSrcFiles, @favoritesOnly, @seed, @clanId);",
            new
            {
                userId,
                personId,
                offset,
                limit = limit + 1,
                excludeSrcFiles = true,
                favoritesOnly,
                seed,
                clanId
            },
            token
        );

        var media = (await AssembleMedia(userId, results, baseUrl, _assetPathBuilder, _cache, token)).ToList();
        var hasMore = media.Count > limit;

        return new SearchResult<Media>(
            hasMore ? media.Take(limit) : media,
            hasMore,
            hasMore ? offset + limit : 0
        );
    }

    // cached because it guards a static asset: a page showing the picker asks for
    // hundreds of face images at once, and without this each one would be its own
    // query.  GetPersons primes the same keys, so the common path never reaches
    // postgres at all.
    public async ValueTask<bool> CanViewFace(
        Guid userId,
        Guid faceId,
        CancellationToken token = default
    ) => await _cache.GetOrCreateAsync(
        CacheKeyBuilder.CanViewFace(userId, faceId),
        async cancel => await ExecuteScalar<bool>(
            "SELECT * FROM media.get_user_can_view_face(@userId, @faceId);",
            new
            {
                userId,
                faceId
            },
            cancel
        ),
        cancellationToken: token
    );

    public async Task<IEnumerable<Clan>> GetClans(
        Guid userId,
        string baseUrl,
        CancellationToken token = default
    ) => await InternalGetClans(userId, baseUrl, null, token);

    public async Task<Clan?> GetClan(
        Guid userId,
        string baseUrl,
        Guid clanId,
        CancellationToken token = default
    ) => (await InternalGetClans(userId, baseUrl, clanId, token))
        .SingleOrDefault();

    public async Task<(Guid? ClanId, ClanOutcome Outcome)> CreateClan(
        Guid userId,
        string name,
        Guid[] personIds,
        CancellationToken token = default
    )
    {
        var rows = await ExecuteQueryInTransaction<CreateClanRow>(
            "SELECT * FROM media.create_clan(@userId, @name, @personIds);",
            new
            {
                userId,
                name,
                personIds
            },
            token
        );

        var row = rows?.SingleOrDefault();

        return row == null
            ? (null, ClanOutcome.NotFound)
            : (row.ClanId, (ClanOutcome)row.Result);
    }

    public async Task<ClanOutcome> UpdateClan(
        Guid userId,
        Guid clanId,
        string name,
        CancellationToken token = default
    ) => await Outcome(
        "SELECT * FROM media.update_clan(@userId, @clanId, @name);",
        new
        {
            userId,
            clanId,
            name
        },
        token
    );

    public async Task<ClanOutcome> SetClanPersons(
        Guid userId,
        Guid clanId,
        Guid[] personIds,
        CancellationToken token = default
    ) => await Outcome(
        "SELECT * FROM media.set_clan_persons(@userId, @clanId, @personIds);",
        new
        {
            userId,
            clanId,
            personIds
        },
        token
    );

    public async Task<ClanOutcome> DeleteClan(
        Guid userId,
        Guid clanId,
        CancellationToken token = default
    ) => await Outcome(
        "SELECT * FROM media.delete_clan(@userId, @clanId);",
        new
        {
            userId,
            clanId
        },
        token
    );

    // the clan functions all return the same integer vocabulary, so the mapping
    // lives in one place rather than being restated at each call site
    async Task<ClanOutcome> Outcome(string sql, object param, CancellationToken token) =>
        (ClanOutcome)await ExecuteScalarInTransaction<int>(sql, param, token);

    async Task<IEnumerable<Clan>> InternalGetClans(
        Guid userId,
        string baseUrl,
        Guid? clanId,
        CancellationToken token
    )
    {
        var rows = await Query<ClanRow>(
            "SELECT * FROM media.get_clans(@userId, @clanId);",
            new
            {
                userId,
                clanId
            },
            token
        );

        // one row per (clan, member), with the person columns null for a clan
        // whose members the caller can no longer see - so the null check is what
        // keeps an empty clan an empty clan rather than a clan with one phantom
        // member.  the sql already ordered the rows; GroupBy preserves that.
        return rows
            .GroupBy(r => r.ClanId)
            .Select(g => new Clan(
                g.Key,
                g.First().ClanName,
                g.First().Created,
                g.First().Modified,
                g
                    .Where(r => r.PersonId != null)
                    .Select(r => ToPerson(r, baseUrl))
                    .ToList()
            ))
            .ToList();
    }

    Person ToPerson(ClanRow row, string baseUrl) =>
        new(
            row.PersonId!.Value,
            row.PersonName!,
            row.PersonSlug,
            row.PreferredFaceId,
            FaceImageUrl(row.PreferredFaceId, baseUrl),
            row.MediaCount ?? 0,
            row.IsFavorite ?? false
        );

    string? FaceImageUrl(Guid? preferredFaceId, string baseUrl) =>
        preferredFaceId == null
            ? null
            : _assetPathBuilder.Build(
                baseUrl,
                string.Format(
                    CultureInfo.InvariantCulture,
                    Constants.FaceImageUrlFormat,
                    preferredFaceId
                )
            );

    // function is a compile time constant from the callers above, never caller
    // input, so interpolating it carries no injection risk
    async Task<IEnumerable<FaceSyncResult>> Sync<T>(
        string function,
        Guid userId,
        IEnumerable<T> items,
        CancellationToken token
    )
    {
        ArgumentNullException.ThrowIfNull(items);

        var payload = JsonSerializer.Serialize(items, PayloadOptions);

        // in a transaction so a batch lands whole: a partial apply would leave
        // the publisher unable to tell what to resend
        var results = await ExecuteQueryInTransaction<FaceSyncResult>(
            $"SELECT * FROM {function}(@userId, @payload::jsonb);",
            new
            {
                userId,
                payload
            },
            token
        );

        return results ?? [];
    }
}
