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
            $"https://example.com/api/v1/faces/{Constants.FACE_SHARED_TRAVEL}/image",
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

    static Person Shared(IEnumerable<Person> people) =>
        people.Single(p => p.Id == Constants.PERSON_SHARED);

    FaceRepository GetRepo() =>
        new(
            new FakeLogger<FaceRepository>(),
            _fixture.DataSource.CreateConnection(),
            new AssetPathBuilder()
        );
}
