using Microsoft.Extensions.Logging.Testing;

namespace MawMedia.Services.Tests;

public class MediaRepositoryTests
{
    readonly TestFixture _fixture;

    public MediaRepositoryTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    public static TheoryData<Guid, byte, int> GetRandomMediaData => new()
    {
        { Guid.CreateVersion7(),  10,   0 },
        { Constants.USER_ADMIN,   1,    1 },
        { Constants.USER_ADMIN,   10,   3 },
        { Constants.USER_JOHNDOE, 1,    1 },
        { Constants.USER_JOHNDOE, 200,  1}
    };

    [Theory]
    [MemberData(nameof(GetRandomMediaData))]
    public async Task GetRandomMedia(Guid userId, byte count, int expectedCount)
    {
        var repo = GetRepo();

        var media = await repo.GetRandomMedia(userId, count);

        Assert.NotNull(media);

        if (expectedCount == 0)
        {
            Assert.Empty(media);
        }
        else
        {
            Assert.Equal(expectedCount, media.Count());
        }
    }

    MediaRepository GetRepo()
    {
        return new MediaRepository(
            new FakeLogger<MediaRepository>(),
            _fixture.DataSource.CreateConnection()
        );
    }
}
