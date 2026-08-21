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
            Constants.USER_ADMIN, "https://example.com", TestContext.Current.CancellationToken);

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
            Constants.USER_JOHNDOE, "https://example.com", TestContext.Current.CancellationToken);

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
        var forAdmin = await repo.GetPersons(Constants.USER_ADMIN, "https://example.com", token);
        var forFriend = await repo.GetPersons(Constants.USER_JOHNDOE, "https://example.com", token);

        Assert.Equal(2, Shared(forAdmin).MediaCount);

        // the count must not leak the media the friend cannot see
        Assert.Equal(1, Shared(forFriend).MediaCount);
    }

    [Fact]
    public async Task PreferredFaceUrlIsAbsoluteAndNullWhenUnset()
    {
        var people = await GetRepo().GetPersons(
            Constants.USER_ADMIN, "https://example.com/", TestContext.Current.CancellationToken);

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
    public async Task PeopleAreOrderedByMediaCountThenName()
    {
        var people = (await GetRepo().GetPersons(
            Constants.USER_ADMIN, "https://example.com", TestContext.Current.CancellationToken)).ToList();

        var counts = people.Select(p => p.MediaCount).ToList();

        Assert.Equal(counts.OrderByDescending(c => c), counts);
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
    public async Task PersonMediaIsScopedToTheCaller()
    {
        var token = TestContext.Current.CancellationToken;
        var repo = GetRepo();

        var forAdmin = await repo.GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, 0, 24, token);
        var forFriend = await repo.GetPersonMedia(
            Constants.USER_JOHNDOE, "https://example.com", Constants.PERSON_SHARED, 0, 24, token);

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
            Constants.USER_JOHNDOE, "https://example.com", Constants.PERSON_PRIVATE, 0, 24,
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Results);
        Assert.False(result.HasMoreResults);
    }

    [Fact]
    public async Task PersonMediaIsEmptyForAPersonThatDoesNotExist()
    {
        var result = await GetRepo().GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Guid.CreateVersion7(), 0, 24,
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task PersonMediaCarriesItsCategoryAndFiles()
    {
        var result = await GetRepo().GetPersonMedia(
            Constants.USER_JOHNDOE, "https://example.com", Constants.PERSON_SHARED, 0, 24,
            TestContext.Current.CancellationToken);

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
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, 0, 1, token);

        Assert.True(first.HasMoreResults);
        Assert.Equal(1, first.NextOffset);

        // newest category first, so the 2023 travel photo leads the 2022 nature one
        var head = Assert.Single(first.Results);
        Assert.Equal(Constants.MEDIA_TRAVEL_1.Id, head.Id);

        // the extra row fetched to detect "more" must not leak into the page, and
        // the file collection must survive being paged
        Assert.Single(head.Files);

        var second = await repo.GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, first.NextOffset, 1, token);

        Assert.False(second.HasMoreResults);
        Assert.Equal(0, second.NextOffset);
        Assert.Equal(Constants.MEDIA_NATURE_1.Id, Assert.Single(second.Results).Id);
    }

    [Theory]
    [InlineData(-1, 24)]
    [InlineData(0, 0)]
    [InlineData(0, FaceRepository.PERSON_MEDIA_LIMIT_MAX + 1)]
    public async Task PersonMediaRejectsAnOutOfRangePage(int offset, int limit)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GetRepo().GetPersonMedia(
            Constants.USER_ADMIN, "https://example.com", Constants.PERSON_SHARED, offset, limit,
            TestContext.Current.CancellationToken));
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
