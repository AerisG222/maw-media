using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MawMedia.Models.FaceRecognition;
using MawMedia.Services.Abstractions;
using MawMedia.Services.Models;
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

    readonly IAssetPathBuilder _assetPathBuilder;

    public FaceRepository(
        ILogger<FaceRepository> log,
        NpgsqlConnection conn,
        IAssetPathBuilder assetPathBuilder
    ) : base(log, conn)
    {
        ArgumentNullException.ThrowIfNull(assetPathBuilder);

        _assetPathBuilder = assetPathBuilder;
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
        CancellationToken token = default
    )
    {
        var rows = await Query<PersonRow>(
            "SELECT * FROM media.get_persons(@userId);",
            new
            {
                userId
            },
            token
        );

        return rows
            .Select(r => new Person(
                r.Id,
                r.Name,
                r.Slug,
                r.PreferredFaceId,
                r.PreferredFaceId == null
                    ? null
                    : _assetPathBuilder.Build(
                        baseUrl,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            Constants.FaceImageUrlFormat,
                            r.PreferredFaceId
                        )
                    ),
                r.MediaCount
            ))
            .ToList();
    }

    public async Task<bool> CanViewFace(
        Guid userId,
        Guid faceId,
        CancellationToken token = default
    ) => await ExecuteScalar<bool>(
        "SELECT * FROM media.get_user_can_view_face(@userId, @faceId);",
        new
        {
            userId,
            faceId
        },
        token
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
