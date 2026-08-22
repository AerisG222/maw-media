using MawMedia.Models.FaceRecognition;
using MawMedia.Services;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace MawMedia.Services.Tests;

// clans are created and torn down per test rather than seeded: they are the
// caller's own data, every test here mutates them, and test classes run in
// parallel - a shared clan would be a flake waiting to happen.
public class ClanTests
{
    const string BASE_URL = "https://example.com";

    readonly TestFixture _fixture;

    public ClanTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task ACreatedClanHoldsItsMembers()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var name = UniqueName();

        var (clanId, outcome) = await repo.CreateClan(
            Constants.USER_ADMIN, name, [Constants.PERSON_SHARED, Constants.PERSON_PRIVATE], token);

        Assert.Equal(ClanOutcome.Applied, outcome);
        Assert.NotNull(clanId);

        try
        {
            var clan = await repo.GetClan(Constants.USER_ADMIN, BASE_URL, clanId.Value, token);

            Assert.Equal(name, clan!.Name);
            Assert.Equal(2, clan.Members.Count());

            // members come back as full people, so a client can draw the clan
            // with faces without a second call
            var shared = clan.Members.Single(m => m.Id == Constants.PERSON_SHARED);

            Assert.Equal(2, shared.MediaCount);
            Assert.EndsWith($"/assets/faces/{Constants.FACE_SHARED_TRAVEL}.avif", shared.PreferredFaceUrl);
        }
        finally
        {
            await repo.DeleteClan(Constants.USER_ADMIN, clanId.Value, token);
        }
    }

    [Fact]
    public async Task AClanCannotHoldAPersonTheCallerCannotSee()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var name = UniqueName();

        // the private person is only in a category ROLE_FRIEND cannot reach
        var (clanId, outcome) = await repo.CreateClan(
            Constants.USER_JOHNDOE, name, [Constants.PERSON_PRIVATE], token);

        Assert.Equal(ClanOutcome.UnknownPerson, outcome);
        Assert.Null(clanId);

        // nothing was written - a rejected create must not leave a clan behind.
        // asserted by name rather than emptiness: other test classes create clans
        // for this user in parallel.
        var clans = await repo.GetClans(Constants.USER_JOHNDOE, BASE_URL, token);

        Assert.DoesNotContain(clans, c => c.Name == name);
    }

    [Fact]
    public async Task AClanNameIsUniquePerOwnerButNotAcrossOwners()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var name = UniqueName();

        var (mine, _) = await repo.CreateClan(Constants.USER_ADMIN, name, [], token);
        var (theirs, theirOutcome) = await repo.CreateClan(Constants.USER_JOHNDOE, name, [], token);

        try
        {
            // the same name belongs to a different owner, so it is not a collision
            Assert.Equal(ClanOutcome.Applied, theirOutcome);

            var (duplicate, outcome) = await repo.CreateClan(Constants.USER_ADMIN, name.ToUpperInvariant(), [], token);

            // compared case insensitively: the picker offers these by name, and
            // two that differ only in case are indistinguishable there
            Assert.Equal(ClanOutcome.DuplicateName, outcome);
            Assert.Null(duplicate);
        }
        finally
        {
            await repo.DeleteClan(Constants.USER_ADMIN, mine!.Value, token);
            await repo.DeleteClan(Constants.USER_JOHNDOE, theirs!.Value, token);
        }
    }

    [Fact]
    public async Task MembershipIsReplacedWholesale()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var (clanId, _) = await repo.CreateClan(
            Constants.USER_ADMIN, UniqueName(), [Constants.PERSON_SHARED], token);

        try
        {
            Assert.Equal(
                ClanOutcome.Applied,
                await repo.SetClanPersons(Constants.USER_ADMIN, clanId!.Value, [Constants.PERSON_PRIVATE], token));

            var clan = await repo.GetClan(Constants.USER_ADMIN, BASE_URL, clanId.Value, token);

            // a replace, not a merge: the person that was there is gone
            Assert.Equal(Constants.PERSON_PRIVATE, Assert.Single(clan!.Members).Id);

            // and sending the same set again is a no-op rather than an error
            Assert.Equal(
                ClanOutcome.Applied,
                await repo.SetClanPersons(Constants.USER_ADMIN, clanId.Value, [Constants.PERSON_PRIVATE], token));

            // an empty set empties the clan without removing it
            Assert.Equal(
                ClanOutcome.Applied,
                await repo.SetClanPersons(Constants.USER_ADMIN, clanId.Value, [], token));

            var emptied = await repo.GetClan(Constants.USER_ADMIN, BASE_URL, clanId.Value, token);

            Assert.NotNull(emptied);
            Assert.Empty(emptied.Members);
        }
        finally
        {
            await repo.DeleteClan(Constants.USER_ADMIN, clanId!.Value, token);
        }
    }

    [Fact]
    public async Task ARejectedMembershipChangeLeavesTheClanAlone()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var (clanId, _) = await repo.CreateClan(
            Constants.USER_JOHNDOE, UniqueName(), [Constants.PERSON_SHARED], token);

        try
        {
            Assert.Equal(
                ClanOutcome.UnknownPerson,
                await repo.SetClanPersons(
                    Constants.USER_JOHNDOE, clanId!.Value,
                    [Constants.PERSON_SHARED, Constants.PERSON_PRIVATE], token));

            // one bad id rejects the whole call rather than being dropped, so the
            // membership is exactly what it was
            var clan = await repo.GetClan(Constants.USER_JOHNDOE, BASE_URL, clanId.Value, token);

            Assert.Equal(Constants.PERSON_SHARED, Assert.Single(clan!.Members).Id);
        }
        finally
        {
            await repo.DeleteClan(Constants.USER_JOHNDOE, clanId!.Value, token);
        }
    }

    [Fact]
    public async Task AClanIsPrivateToItsOwner()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var (clanId, _) = await repo.CreateClan(
            Constants.USER_ADMIN, UniqueName(), [Constants.PERSON_SHARED], token);

        try
        {
            // johndoe can see PERSON_SHARED, but the clan is not theirs - and the
            // answer is indistinguishable from the clan not existing
            Assert.Null(await repo.GetClan(Constants.USER_JOHNDOE, BASE_URL, clanId!.Value, token));

            Assert.Equal(
                ClanOutcome.NotFound,
                await repo.UpdateClan(Constants.USER_JOHNDOE, clanId.Value, UniqueName(), token));

            Assert.Equal(
                ClanOutcome.NotFound,
                await repo.SetClanPersons(Constants.USER_JOHNDOE, clanId.Value, [], token));

            Assert.Equal(
                ClanOutcome.NotFound,
                await repo.DeleteClan(Constants.USER_JOHNDOE, clanId.Value, token));

            // still there, untouched
            Assert.NotNull(await repo.GetClan(Constants.USER_ADMIN, BASE_URL, clanId.Value, token));
        }
        finally
        {
            await repo.DeleteClan(Constants.USER_ADMIN, clanId!.Value, token);
        }
    }

    [Fact]
    public async Task RenamingLeavesMembershipAlone()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var renamed = UniqueName();

        var (clanId, _) = await repo.CreateClan(
            Constants.USER_ADMIN, UniqueName(), [Constants.PERSON_SHARED], token);

        try
        {
            Assert.Equal(
                ClanOutcome.Applied,
                await repo.UpdateClan(Constants.USER_ADMIN, clanId!.Value, renamed, token));

            var clan = await repo.GetClan(Constants.USER_ADMIN, BASE_URL, clanId.Value, token);

            Assert.Equal(renamed, clan!.Name);
            Assert.Equal(Constants.PERSON_SHARED, Assert.Single(clan.Members).Id);
        }
        finally
        {
            await repo.DeleteClan(Constants.USER_ADMIN, clanId!.Value, token);
        }
    }

    [Fact]
    public async Task DeletingAClanLeavesItsPeopleAlone()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var (clanId, _) = await repo.CreateClan(
            Constants.USER_ADMIN, UniqueName(), [Constants.PERSON_SHARED], token);

        Assert.Equal(ClanOutcome.Applied, await repo.DeleteClan(Constants.USER_ADMIN, clanId!.Value, token));
        Assert.Null(await repo.GetClan(Constants.USER_ADMIN, BASE_URL, clanId.Value, token));

        // a clan is only ever a saved selection
        var people = await repo.GetPersons(Constants.USER_ADMIN, BASE_URL, token: token);

        Assert.Contains(people, p => p.Id == Constants.PERSON_SHARED);
    }

    [Fact]
    public async Task ClanMediaIsTheUnionOfItsMembers()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var (clanId, _) = await repo.CreateClan(
            Constants.USER_ADMIN, UniqueName(),
            [Constants.PERSON_SHARED, Constants.PERSON_PRIVATE], token);

        try
        {
            var media = await repo.GetClanMedia(Constants.USER_ADMIN, BASE_URL, clanId!.Value, 0, 24, token: token);

            // the shared person is in both photos and the private one is in the
            // nature photo, so the union is two - the nature photo holding two
            // members still counts once
            Assert.Equal(
                [Constants.MEDIA_TRAVEL_1.Id, Constants.MEDIA_NATURE_1.Id],
                media.Results.Select(m => m.Id).ToList()
            );
        }
        finally
        {
            await repo.DeleteClan(Constants.USER_ADMIN, clanId!.Value, token);
        }
    }

    [Fact]
    public async Task ClanMediaKeepsThePagingFavoritesAndSeedBehaviour()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var (clanId, _) = await repo.CreateClan(
            Constants.USER_ADMIN, UniqueName(),
            [Constants.PERSON_SHARED, Constants.PERSON_PRIVATE], token);

        try
        {
            var page = await repo.GetClanMedia(Constants.USER_ADMIN, BASE_URL, clanId!.Value, 0, 1, token: token);

            Assert.True(page.HasMoreResults);
            Assert.Equal(1, page.NextOffset);
            Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, Assert.Single(page.Results).Id);

            var favorites = await repo.GetClanMedia(
                Constants.USER_ADMIN, BASE_URL, clanId.Value, 0, 24, favoritesOnly: true, token: token);

            Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, Assert.Single(favorites.Results).Id);

            var shuffled = await repo.GetClanMedia(
                Constants.USER_ADMIN, BASE_URL, clanId.Value, 0, 24, seed: 12345, token: token);

            var again = await repo.GetClanMedia(
                Constants.USER_ADMIN, BASE_URL, clanId.Value, 0, 24, seed: 12345, token: token);

            Assert.Equal(
                shuffled.Results.Select(m => m.Id).ToList(),
                again.Results.Select(m => m.Id).ToList()
            );
        }
        finally
        {
            await repo.DeleteClan(Constants.USER_ADMIN, clanId!.Value, token);
        }
    }

    [Fact]
    public async Task ClanMediaIsEmptyForSomeoneElsesClan()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var (clanId, _) = await repo.CreateClan(
            Constants.USER_ADMIN, UniqueName(), [Constants.PERSON_SHARED], token);

        try
        {
            // johndoe can see the member, but not through a clan that is not theirs
            var media = await repo.GetClanMedia(Constants.USER_JOHNDOE, BASE_URL, clanId!.Value, 0, 24, token: token);

            Assert.Empty(media.Results);
        }
        finally
        {
            await repo.DeleteClan(Constants.USER_ADMIN, clanId!.Value, token);
        }
    }

    [Fact]
    public async Task DeletingAPersonRemovesThemFromClans()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var personId = Guid.CreateVersion7();
        var faceId = Guid.CreateVersion7();

        await repo.SyncPersons(
            Constants.USER_ADMIN,
            [new PersonSync(personId, "clan-cascade", null, null, null, 1, 1, NodaTime.Instant.FromDateTimeUtc(DateTime.UtcNow))],
            token);

        await repo.SyncFaces(
            Constants.USER_ADMIN,
            [new FaceSync(faceId, Constants.FILE_NATURE_1.Path, personId, 0.1m, 0.2m, 0.3m, 0.4m, 0.9f, 1)],
            token);

        var (clanId, outcome) = await repo.CreateClan(Constants.USER_ADMIN, UniqueName(), [personId], token);

        Assert.Equal(ClanOutcome.Applied, outcome);

        try
        {
            // the whole reason clan_person cascades: a cluster that no longer
            // exists upstream has to be deletable, and a clan membership must not
            // be able to block that
            await repo.DeletePersons(Constants.USER_ADMIN, [personId], token);

            var clan = await repo.GetClan(Constants.USER_ADMIN, BASE_URL, clanId!.Value, token);

            Assert.NotNull(clan);
            Assert.Empty(clan.Members);
        }
        finally
        {
            await repo.DeleteClan(Constants.USER_ADMIN, clanId!.Value, token);
        }
    }

    static string UniqueName() => $"clan-{Guid.CreateVersion7()}";

    FaceRepository GetRepo() =>
        new(
            new FakeLogger<FaceRepository>(),
            _fixture.DataSource.CreateConnection(),
            new AssetPathBuilder(),
            new FakeHybridCache()
        );
}
