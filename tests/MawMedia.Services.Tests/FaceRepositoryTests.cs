using Dapper;
using MawMedia.Models.FaceRecognition;
using Microsoft.Extensions.Logging.Testing;
using NodaTime;

namespace MawMedia.Services.Tests;

public class FaceRepositoryTests
{
    readonly TestFixture _fixture;

    public FaceRepositoryTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task SyncIsRejectedForNonAdmin()
    {
        var repo = GetRepo();
        var token = TestContext.Current.CancellationToken;

        // every entry point guards independently - there is no shared gate now
        // that the batch is split across four calls
        Assert.Equal("forbidden", OnlyOutcome(await repo.SyncPersonStatuses(
            Constants.USER_JOHNDOE, [new PersonStatusSync("nope", "Nope", null, 1)], token)));

        Assert.Equal("forbidden", OnlyOutcome(await repo.SyncPersons(
            Constants.USER_JOHNDOE, [NewPerson(Guid.CreateVersion7(), "nope", 1)], token)));

        Assert.Equal("forbidden", OnlyOutcome(await repo.SyncFaces(
            Constants.USER_JOHNDOE, [NewFace(Guid.CreateVersion7(), null, Constants.FILE_NATURE_1.Path, 1)], token)));

        Assert.Equal("forbidden", OnlyOutcome(await repo.SyncDeletions(
            Constants.USER_JOHNDOE, [new EntitySyncDeletion(SyncEntityType.Person, Guid.CreateVersion7())], token)));

        Assert.Equal(0, await CountPersons("nope"));
    }

    [Fact]
    public async Task SyncAppliesStatusPersonAndFaceInOrder()
    {
        var repo = GetRepo();
        var personId = Guid.CreateVersion7();
        var faceId = Guid.CreateVersion7();
        var token = TestContext.Current.CancellationToken;

        var statuses = await repo.SyncPersonStatuses(
            Constants.USER_ADMIN, [new PersonStatusSync("unknown", "Unknown", "not named", 1)], token);

        Assert.Equal("applied", OnlyOutcome(statuses));

        var persons = await repo.SyncPersons(
            Constants.USER_ADMIN, [NewPerson(personId, "sync-applies", 1)], token);

        Assert.Equal("applied", OutcomeFor(persons, personId));

        var faces = await repo.SyncFaces(
            Constants.USER_ADMIN, [NewFace(faceId, personId, Constants.FILE_NATURE_1.Path, 1)], token);

        Assert.Equal("applied", OutcomeFor(faces, faceId));

        // the published file path must resolve to the media that owns that file
        var mediaId = await QuerySingle<Guid?>(
            "SELECT media_id FROM media.face WHERE id = @faceId;",
            new { faceId }
        );

        Assert.Equal(Constants.MEDIA_NATURE_1.Id, mediaId);
    }

    [Fact]
    public async Task SyncReportsUnresolvedPathWithoutFailingTheBatch()
    {
        var repo = GetRepo();
        var personId = Guid.CreateVersion7();
        var goodFaceId = Guid.CreateVersion7();
        var badFaceId = Guid.CreateVersion7();
        const string missingPath = "/media/does-not-exist.jpg";
        var token = TestContext.Current.CancellationToken;

        await repo.SyncPersons(Constants.USER_ADMIN, [NewPerson(personId, "sync-unresolved", 1)], token);

        var faces = await repo.SyncFaces(
            Constants.USER_ADMIN,
            [
                NewFace(goodFaceId, personId, Constants.FILE_NATURE_1.Path, 1),
                NewFace(badFaceId, personId, missingPath, 1)
            ],
            token
        );

        // the resolvable face still lands - one bad path does not sink the batch
        Assert.Equal("applied", OutcomeFor(faces, goodFaceId));

        var unresolved = Assert.Single(faces, r => r.EntityId == badFaceId);

        Assert.Equal("unresolved_path", unresolved.Outcome);
        Assert.Equal(missingPath, unresolved.Detail);

        Assert.Equal(0, await CountFaces(badFaceId));
    }

    [Fact]
    public async Task SyncReportsAFaceWhosePersonHasNotBeenPublishedYet()
    {
        var repo = GetRepo();
        var knownPersonId = Guid.CreateVersion7();
        var strayPersonId = Guid.CreateVersion7();
        var goodFaceId = Guid.CreateVersion7();
        var orphanFaceId = Guid.CreateVersion7();
        var token = TestContext.Current.CancellationToken;

        await repo.SyncPersons(Constants.USER_ADMIN, [NewPerson(knownPersonId, "sync-known", 1)], token);

        // persons and faces are separate transactions now, so a face arriving
        // before its person is a realistic sequencing error.  it must report per
        // face rather than raise a foreign key violation over the whole batch.
        var faces = await repo.SyncFaces(
            Constants.USER_ADMIN,
            [
                NewFace(goodFaceId, knownPersonId, Constants.FILE_NATURE_1.Path, 1),
                NewFace(orphanFaceId, strayPersonId, Constants.FILE_NATURE_1.Path, 1)
            ],
            token
        );

        Assert.Equal("applied", OutcomeFor(faces, goodFaceId));

        var orphan = Assert.Single(faces, r => r.EntityId == orphanFaceId);

        Assert.Equal("unknown_person", orphan.Outcome);
        Assert.Equal(strayPersonId.ToString(), orphan.Detail);

        Assert.Equal(0, await CountFaces(orphanFaceId));
    }

    [Fact]
    public async Task SyncAcceptsAnUnassignedFace()
    {
        var repo = GetRepo();
        var faceId = Guid.CreateVersion7();

        // a null person_id is not the same as an unknown one - unclustered faces
        // are normal and must still land
        var faces = await repo.SyncFaces(
            Constants.USER_ADMIN,
            [NewFace(faceId, null, Constants.FILE_NATURE_1.Path, 1)],
            TestContext.Current.CancellationToken
        );

        Assert.Equal("applied", OutcomeFor(faces, faceId));
        Assert.Equal(1, await CountFaces(faceId));
    }

    [Fact]
    public async Task SyncSkipsRevisionsThatAreNotNewer()
    {
        var repo = GetRepo();
        var personId = Guid.CreateVersion7();
        var token = TestContext.Current.CancellationToken;

        await repo.SyncPersons(Constants.USER_ADMIN, [NewPerson(personId, "sync-rev-first", 10)], token);

        // replaying the same revision is a no-op, which is what makes the
        // publisher's at-least-once delivery safe
        var replay = await repo.SyncPersons(
            Constants.USER_ADMIN, [NewPerson(personId, "sync-rev-replay", 10)], token);

        Assert.Equal("skipped_stale", OutcomeFor(replay, personId));
        Assert.Equal("sync-rev-first", await NameOf(personId));

        // an older revision arriving late is also ignored
        var older = await repo.SyncPersons(
            Constants.USER_ADMIN, [NewPerson(personId, "sync-rev-older", 9)], token);

        Assert.Equal("skipped_stale", OutcomeFor(older, personId));
        Assert.Equal("sync-rev-first", await NameOf(personId));

        var newer = await repo.SyncPersons(
            Constants.USER_ADMIN, [NewPerson(personId, "sync-rev-newer", 11)], token);

        Assert.Equal("applied", OutcomeFor(newer, personId));
        Assert.Equal("sync-rev-newer", await NameOf(personId));
    }

    [Fact]
    public async Task SyncStoresAPreferredFaceThatDoesNotExistYet()
    {
        var repo = GetRepo();
        var personId = Guid.CreateVersion7();
        var faceId = Guid.CreateVersion7();
        var token = TestContext.Current.CancellationToken;

        // preferred_face_id has no foreign key precisely because the face is
        // published in a later call than the person that names it
        var persons = await repo.SyncPersons(
            Constants.USER_ADMIN,
            [NewPerson(personId, "sync-preferred", 1) with { PreferredFaceId = faceId }],
            token
        );

        Assert.Equal("applied", OutcomeFor(persons, personId));

        var preferred = await QuerySingle<Guid?>(
            "SELECT preferred_face_id FROM media.person WHERE id = @personId;",
            new { personId }
        );

        Assert.Equal(faceId, preferred);

        var faces = await repo.SyncFaces(
            Constants.USER_ADMIN, [NewFace(faceId, personId, Constants.FILE_NATURE_1.Path, 1)], token);

        Assert.Equal("applied", OutcomeFor(faces, faceId));
    }

    [Fact]
    public async Task SyncCollapsesADuplicatedIdKeepingTheHighestRevision()
    {
        var repo = GetRepo();
        var personId = Guid.CreateVersion7();

        var results = await repo.SyncPersons(
            Constants.USER_ADMIN,
            [
                NewPerson(personId, "sync-dupe-low", 5),
                NewPerson(personId, "sync-dupe-high", 6)
            ],
            TestContext.Current.CancellationToken
        );

        // one row back, not two, and no "cannot affect row a second time" error
        var result = Assert.Single(results, r => r.EntityId == personId);

        Assert.Equal("applied", result.Outcome);
        Assert.Equal("sync-dupe-high", await NameOf(personId));
    }

    [Fact]
    public async Task SyncDeletesAndALaterPublishRecreates()
    {
        var repo = GetRepo();
        var personId = Guid.CreateVersion7();
        var faceId = Guid.CreateVersion7();
        var token = TestContext.Current.CancellationToken;

        await repo.SyncPersons(Constants.USER_ADMIN, [NewPerson(personId, "sync-delete", 1)], token);
        await repo.SyncFaces(
            Constants.USER_ADMIN, [NewFace(faceId, personId, Constants.FILE_NATURE_1.Path, 1)], token);

        var deletions = await repo.SyncDeletions(
            Constants.USER_ADMIN,
            [
                new EntitySyncDeletion(SyncEntityType.Face, faceId),
                new EntitySyncDeletion(SyncEntityType.Person, personId),
                new EntitySyncDeletion(SyncEntityType.Person, Guid.CreateVersion7())
            ],
            token
        );

        Assert.Equal("deleted", OutcomeFor(deletions, personId));
        Assert.Equal("deleted", OutcomeFor(deletions, faceId));
        Assert.Single(deletions, r => r.Outcome == "not_found");

        // hard delete - maw-media-ai is the system of record, nothing is kept here
        Assert.Equal(0, await CountPersons("sync-delete"));
        Assert.Equal(0, await CountFaces(faceId));

        // and a later publish simply recreates it
        await repo.SyncPersons(Constants.USER_ADMIN, [NewPerson(personId, "sync-delete", 2)], token);

        Assert.Equal(1, await CountPersons("sync-delete"));
    }

    [Fact]
    public async Task DeletingAPersonLeavesItsFacesUnassigned()
    {
        var repo = GetRepo();
        var personId = Guid.CreateVersion7();
        var faceId = Guid.CreateVersion7();
        var token = TestContext.Current.CancellationToken;

        await repo.SyncPersons(Constants.USER_ADMIN, [NewPerson(personId, "sync-orphan", 1)], token);
        await repo.SyncFaces(
            Constants.USER_ADMIN, [NewFace(faceId, personId, Constants.FILE_NATURE_1.Path, 1)], token);

        await repo.SyncDeletions(
            Constants.USER_ADMIN, [new EntitySyncDeletion(SyncEntityType.Person, personId)], token);

        // ON DELETE SET NULL: dropping a cluster unassigns its faces rather than
        // deleting them, matching face_detection.person_id upstream
        Assert.Equal(1, await CountFaces(faceId));

        var stillAssigned = await QuerySingle<bool>(
            "SELECT person_id IS NOT NULL FROM media.face WHERE id = @faceId;",
            new { faceId }
        );

        Assert.False(stillAssigned);
    }

    [Fact]
    public async Task SyncDeletionsStillGuardsAnUnknownEntityTypeFromAnUntypedCaller()
    {
        // EntitySyncDeletion.EntityType is an enum, so the repository can no longer
        // produce this.  the guard still has to hold for callers that reach the
        // function directly - psql, or a future worker.
        var outcome = await QuerySingle<string>(
            """
            SELECT outcome
            FROM media.sync_deletions(
                @userId,
                '[{"entity_type":"llama","id":"1e2d3c4b-0000-7000-8000-00000000ffff"}]'::jsonb
            );
            """,
            new { userId = Constants.USER_ADMIN }
        );

        Assert.Equal("unknown_entity", outcome);
    }

    static PersonSync NewPerson(Guid id, string name, long revision) =>
        new(id, name, null, null, null, 1, revision, Instant.FromDateTimeUtc(DateTime.UtcNow));

    static FaceSync NewFace(Guid id, Guid? personId, string path, long revision) =>
        new(id, path, personId, 0.1m, 0.2m, 0.3m, 0.4m, 0.9f, revision);

    static string? OutcomeFor(IEnumerable<FaceSyncResult> results, Guid entityId) =>
        results.SingleOrDefault(r => r.EntityId == entityId)?.Outcome;

    static string OnlyOutcome(IEnumerable<FaceSyncResult> results) =>
        Assert.Single(results).Outcome;

    async Task<string?> NameOf(Guid personId) =>
        await QuerySingle<string?>("SELECT name FROM media.person WHERE id = @personId;", new { personId });

    async Task<int> CountPersons(string name) =>
        await QuerySingle<int>("SELECT count(*) FROM media.person WHERE name = @name;", new { name });

    async Task<int> CountFaces(Guid faceId) =>
        await QuerySingle<int>("SELECT count(*) FROM media.face WHERE id = @faceId;", new { faceId });

    async Task<T?> QuerySingle<T>(string sql, object param)
    {
        using var conn = _fixture.DataSource.CreateConnection();

        return await conn.QuerySingleOrDefaultAsync<T>(sql, param);
    }

    FaceRepository GetRepo()
    {
        return new FaceRepository(
            new FakeLogger<FaceRepository>(),
            _fixture.DataSource.CreateConnection()
        );
    }
}
