using MawMedia.Models;
using MawMedia.Models.FaceRecognition;
using MawMedia.Services;
using Microsoft.Extensions.Logging.Testing;

namespace MawMedia.Services.Tests;

public class PersonReadTests
{
    readonly TestFixture _fixture;

    public PersonReadTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task AdminSeesEveryPersonInTheirMedia()
    {
        var people = await GetRepo().GetPersons(
            Constants.USER_ADMIN, "https://example.com", token: TestContext.Current.CancellationToken);

        Assert.Contains(people, p => p.Id == Constants.PERSON_SHARED);
        Assert.Contains(people, p => p.Id == Constants.PERSON_PRIVATE);
    }

    [Fact]
    public async Task AUserOnlySeesPeopleAppearingInMediaTheyCanAccess()
    {
        // johndoe holds ROLE_FRIEND, which reaches CATEGORY_TRAVEL only.  the
        // private person appears solely in a nature photo, so must be invisible -
        // not merely hidden, but absent from the list entirely
        var people = await GetRepo().GetPersons(
            Constants.USER_JOHNDOE, "https://example.com", token: TestContext.Current.CancellationToken);

        Assert.Contains(people, p => p.Id == Constants.PERSON_SHARED);
        Assert.DoesNotContain(people, p => p.Id == Constants.PERSON_PRIVATE);
    }

    [Fact]
    public async Task MediaCountIsScopedToTheCallerRatherThanThePublishedTotal()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        // the shared person is seeded with face_count = 99 and appears in two
        // media: one nature (admin only) and one travel (admin + friend)
        var forAdmin = await repo.GetPersons(Constants.USER_ADMIN, "https://example.com", token: token);
        var forFriend = await repo.GetPersons(Constants.USER_JOHNDOE, "https://example.com", token: token);

        Assert.Equal(2, Shared(forAdmin).MediaCount);

        // the count must not leak the media the friend cannot see
        Assert.Equal(1, Shared(forFriend).MediaCount);
    }

    [Fact]
    public async Task PreferredFaceUrlIsAbsoluteAndNullWhenUnset()
    {
        var people = await GetRepo().GetPersons(
            Constants.USER_ADMIN, "https://example.com/", token: TestContext.Current.CancellationToken);

        var shared = Shared(people);

        Assert.Equal(
            $"https://example.com/assets/faces/{Constants.FACE_SHARED_TRAVEL}.avif",
            shared.PreferredFaceUrl
        );

        var withoutPreferred = people.Single(p => p.Id == Constants.PERSON_PRIVATE);

        Assert.Null(withoutPreferred.PreferredFaceId);
        Assert.Null(withoutPreferred.PreferredFaceUrl);
    }

    [Fact]
    public async Task PeopleAreOrderedByFavoriteThenMediaCountThenName()
    {
        var people = (await GetRepo().GetPersons(
            Constants.USER_ADMIN, "https://example.com", token: TestContext.Current.CancellationToken)).ToList();

        // asserted against the whole ordering key rather than the counts alone:
        // favourites now sort first, and another test class may hold a favourite
        // for this caller while this runs, which a counts-only assertion would
        // read as a broken order
        Assert.Equal(
            people
                .OrderByDescending(p => p.IsFavorite)
                .ThenByDescending(p => p.MediaCount)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => p.Id)
                .ToList(),
            people.Select(p => p.Id).ToList()
        );
    }

    [Fact]
    public async Task FaceVisibilityFollowsTheSameRuleAsThePersonList()
    {
        var repo = GetRepo();
        var token = TestContext.Current.CancellationToken;

        Assert.True(await repo.CanViewFace(Constants.USER_ADMIN, Constants.FACE_PRIVATE_NATURE, token));

        // the friend cannot reach the nature category, so must not be able to
        // fetch that face's published image even holding its id
        Assert.False(await repo.CanViewFace(Constants.USER_JOHNDOE, Constants.FACE_PRIVATE_NATURE, token));
        Assert.True(await repo.CanViewFace(Constants.USER_JOHNDOE, Constants.FACE_SHARED_TRAVEL, token));
    }

    [Fact]
    public async Task FavoritedPeopleAreFlaggedPerCaller()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var forFriend = await repo.GetPersons(Constants.USER_JOHNDOE, "https://example.com", token: token);
        var forAdmin = await repo.GetPersons(Constants.USER_ADMIN, "https://example.com", token: token);

        Assert.True(Shared(forFriend).IsFavorite);

        // one user's favourite must not show up as anyone else's
        Assert.False(Shared(forAdmin).IsFavorite);
    }

    [Fact]
    public async Task FavoritedPeopleSortAheadOfMoreProlificOnes()
    {
        // the admin sees both people; johndoe only sees the shared one, so the
        // admin is the caller that can demonstrate ordering.  the private person
        // has no favourite and the shared one is favourited for johndoe, so a
        // favourite is added here for the admin to move it ahead of a person it
        // would otherwise tie or trail.
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        Assert.True(await repo.SetPersonIsFavorite(
            Constants.USER_ADMIN, Constants.PERSON_PRIVATE, true, token));

        try
        {
            var people = (await repo.GetPersons(
                Constants.USER_ADMIN, "https://example.com", token: token)).ToList();

            // the private person appears in one media, the shared one in two, so
            // without the favourite it would sort second
            Assert.Equal(Constants.PERSON_PRIVATE, people[0].Id);
            Assert.True(people[0].IsFavorite);
        }
        finally
        {
            await repo.SetPersonIsFavorite(Constants.USER_ADMIN, Constants.PERSON_PRIVATE, false, token);
        }
    }

    [Fact]
    public async Task PeopleCanBeFilteredToFavorites()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var favorites = await repo.GetPersons(
            Constants.USER_JOHNDOE, "https://example.com", favoritesOnly: true, token: token);

        Assert.Contains(favorites, p => p.Id == Constants.PERSON_SHARED);
        Assert.All(favorites, p => Assert.True(p.IsFavorite));

        // the admin has not favourited the shared person.  asserted by identity
        // rather than emptiness: PERSON_TOGGLE is favourited and unfavourited by
        // another test class running in parallel.
        var none = await repo.GetPersons(
            Constants.USER_ADMIN, "https://example.com", favoritesOnly: true, token: token);

        Assert.DoesNotContain(none, p => p.Id == Constants.PERSON_SHARED);
    }

    [Fact]
    public async Task APersonCannotBeFavoritedWhenItIsNotVisible()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        // the private person appears only in a category ROLE_FRIEND cannot reach
        Assert.False(await repo.SetPersonIsFavorite(
            Constants.USER_JOHNDOE, Constants.PERSON_PRIVATE, true, token));

        Assert.False(await repo.SetPersonIsFavorite(
            Constants.USER_ADMIN, Guid.CreateVersion7(), true, token));

        // and nothing was written, so it cannot be read back
        var people = await repo.GetPersons(
            Constants.USER_JOHNDOE, "https://example.com", favoritesOnly: true, token: token);

        Assert.DoesNotContain(people, p => p.Id == Constants.PERSON_PRIVATE);
    }

    [Fact]
    public async Task FavoritingIsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        try
        {
            Assert.True(await repo.SetPersonIsFavorite(Constants.USER_ADMIN, Constants.PERSON_SHARED, true, token));

            // a retried call means the same thing as the first, so it must not
            // fail on the primary key
            Assert.True(await repo.SetPersonIsFavorite(Constants.USER_ADMIN, Constants.PERSON_SHARED, true, token));

            var person = await repo.GetPerson(
                Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, token);

            Assert.True(person!.IsFavorite);
        }
        finally
        {
            await repo.SetPersonIsFavorite(Constants.USER_ADMIN, Constants.PERSON_SHARED, false, token);
        }

        // unfavouriting something that is not favourited is likewise a no-op
        Assert.True(await repo.SetPersonIsFavorite(Constants.USER_ADMIN, Constants.PERSON_SHARED, false, token));
    }

    [Fact]
    public async Task GetPersonAppliesTheSameVisibilityRuleAsTheList()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        Assert.NotNull(await repo.GetPerson(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_PRIVATE, token));

        Assert.Null(await repo.GetPerson(
            Constants.USER_JOHNDOE, "https://example.com", Constants.PERSON_PRIVATE, token));

        Assert.Null(await repo.GetPerson(
            Constants.USER_ADMIN, "https://example.com", Guid.CreateVersion7(), token));
    }

    [Fact]
    public async Task PersonMediaIsScopedToTheCaller()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var forAdmin = await repo.GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, 0, 24, token: token);
        var forFriend = await repo.GetPersonMedia(
            Constants.USER_JOHNDOE, "https://example.com", Constants.PERSON_SHARED, 0, 24, token: token);

        Assert.Equal(
            [Constants.MEDIA_TRAVEL_1.Id, Constants.MEDIA_NATURE_1.Id],
            forAdmin.Results.Select(m => m.Id).ToList()
        );

        // the nature photo is behind a category ROLE_FRIEND cannot reach, so the
        // drill-in must drop it rather than merely hide the face within it
        Assert.Equal(
            [Constants.MEDIA_TRAVEL_1.Id],
            forFriend.Results.Select(m => m.Id).ToList()
        );
    }

    [Fact]
    public async Task PersonMediaIsEmptyForAPersonTheCallerCannotSee()
    {
        var result = await GetRepo().GetPersonMedia(
            Constants.USER_JOHNDOE, "https://example.com", Constants.PERSON_PRIVATE,
            0, 24, token: TestContext.Current.CancellationToken);

        Assert.Empty(result.Results);
        Assert.False(result.HasMoreResults);
    }

    [Fact]
    public async Task PersonMediaIsEmptyForAPersonThatDoesNotExist()
    {
        var result = await GetRepo().GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Guid.CreateVersion7(),
            0, 24, token: TestContext.Current.CancellationToken);

        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task PersonMediaCarriesItsCategoryAndFiles()
    {
        var result = await GetRepo().GetPersonMedia(
            Constants.USER_JOHNDOE, "https://example.com", Constants.PERSON_SHARED,
            0, 24, token: TestContext.Current.CancellationToken);

        var travel = Assert.Single(result.Results);

        Assert.Equal(Constants.CATEGORY_TRAVEL.Id, travel.CategoryId);
        Assert.Equal("travel", travel.CategorySlug);
        Assert.Equal(2023, travel.CategoryYear);

        // assembled the same way every other media read is, so a client can hand
        // the result straight to its existing media component
        var file = Assert.Single(travel.Files);

        Assert.StartsWith("https://example.com/", file.Path);
    }

    [Fact]
    public async Task PersonMediaPagesOverMediaAndReportsMore()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var first = await repo.GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, 0, 1, token: token);

        Assert.True(first.HasMoreResults);
        Assert.Equal(1, first.NextOffset);

        // newest category first, so the 2023 travel photo leads the 2022 nature one
        var head = Assert.Single(first.Results);
        Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, head.Id);

        // the extra row fetched to detect "more" must not leak into the page, and
        // the file collection must survive being paged
        Assert.Single(head.Files);

        var second = await repo.GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, first.NextOffset, 1, token: token);

        Assert.False(second.HasMoreResults);
        Assert.Equal(0, second.NextOffset);
        Assert.Equal(Constants.MEDIA_NATURE_1.Id, Assert.Single(second.Results).Id);
    }

    [Fact]
    public async Task PersonMediaCanBeFilteredToFavorites()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var all = await repo.GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, 0, 24, token: token);

        var favorites = await repo.GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, 0, 24,
            favoritesOnly: true, token: token);

        Assert.Equal(2, all.Results.Count());

        // only the travel photo is favourited by the admin
        var only = Assert.Single(favorites.Results);

        Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, only.Id);
        Assert.True(only.IsFavorite);
    }

    [Fact]
    public async Task PersonMediaFavoritesStillObeyAccess()
    {
        // johndoe favourited the nature photo, which ROLE_FRIEND cannot reach.
        // the filter narrows what is already visible - it must not reach past the
        // access rule to find it.
        var result = await GetRepo().GetPersonMedia(
            Constants.USER_JOHNDOE, "https://example.com", Constants.PERSON_SHARED, 0, 24,
            favoritesOnly: true, token: TestContext.Current.CancellationToken);

        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task PersonMediaWithTheSameSeedReturnsTheSameOrder()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var first = await repo.GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, 0, 24,
            seed: 12345, token: token);

        var again = await repo.GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, 0, 24,
            seed: 12345, token: token);

        Assert.Equal(
            first.Results.Select(m => m.Id).ToList(),
            again.Results.Select(m => m.Id).ToList()
        );
    }

    [Fact]
    public async Task PersonMediaWithASeedPagesWithoutRepeatingOrSkipping()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var seen = new List<Guid>();

        // the whole reason the shuffle takes a seed rather than using RANDOM():
        // walking the pages must visit every media exactly once
        for (var offset = 0; offset < 2; offset++)
        {
            var page = await repo.GetPersonMedia(
                Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, offset, 1,
                seed: 999, token: token);

            seen.AddRange(page.Results.Select(m => m.Id));
        }

        Assert.Equal(2, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Contains(Constants.MEDIA_TRAVEL_1.Id, seen);
        Assert.Contains(Constants.MEDIA_NATURE_1.Id, seen);
    }

    [Fact]
    public async Task PersonMediaSeedActuallyReordersForSomeSeeds()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();
        var leaders = new HashSet<Guid>();

        // hashtextextended is deterministic, so this either always passes or
        // always fails - it is not a coin flip.  it proves the seed is doing
        // something rather than being ignored.
        for (var seed = 1; seed <= 20; seed++)
        {
            var page = await repo.GetPersonMedia(
                Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, 0, 24,
                seed: seed, token: token);

            leaders.Add(page.Results.First().Id);
        }

        Assert.Equal(2, leaders.Count);
    }

    [Theory]
    [InlineData(-1, 24)]
    [InlineData(0, 0)]
    [InlineData(0, FaceRepository.PERSON_MEDIA_LIMIT_MAX + 1)]
    public async Task PersonMediaRejectsAnOutOfRangePage(int offset, int limit)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GetRepo().GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, offset, limit,
            token: TestContext.Current.CancellationToken));
    }

    static Person Shared(IEnumerable<Person> people) =>
        people.Single(p => p.Id == Constants.PERSON_SHARED);

    FaceRepository GetRepo() =>
        new(
            new FakeLogger<FaceRepository>(),
            _fixture.DataSource.CreateConnection(),
            new AssetPathBuilder(),
            new FakeHybridCache()
        );
}
