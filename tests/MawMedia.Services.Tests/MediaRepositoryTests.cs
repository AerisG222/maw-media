using MawMedia.Services.Tests.Models;
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

    public static TheoryData<Guid, Guid, DbMedia?, int> GetMediaData => new()
    {
        { Guid.CreateVersion7(),  Guid.CreateVersion7(),       null, 0 },
        { Guid.CreateVersion7(),  Constants.MEDIA_NATURE_1.Id, null, 0 },
        { Constants.USER_ADMIN,   Guid.CreateVersion7(),       null, 0 },
        { Constants.USER_ADMIN,   Constants.MEDIA_NATURE_1.Id, Constants.MEDIA_NATURE_1, 1 },
        { Constants.USER_JOHNDOE, Constants.MEDIA_NATURE_1.Id, null, 0 }
    };

    [Theory]
    [MemberData(nameof(GetMediaData))]
    public async Task GetMedia(Guid userId, Guid mediaId, DbMedia? expectedMedia, int expectedFileCount)
    {
        var repo = GetRepo();

        var media = await repo.GetMedia(userId, mediaId);

        if (expectedMedia == null)
        {
            Assert.Null(media);
        }
        else
        {
            Assert.NotNull(media);
            Assert.Equal(expectedMedia.Id, media.Id);
            Assert.Equal("photo", media.Type);  // may expand testing in future for more type testing...
            Assert.NotNull(media.Files);
            Assert.Equal(expectedFileCount, media.Files.Count());
        }
    }

    public static TheoryData<Guid, Guid, bool> GetGpsData => new()
    {
        { Guid.CreateVersion7(),  Guid.CreateVersion7(),       true },
        { Guid.CreateVersion7(),  Constants.MEDIA_NATURE_1.Id, true },
        { Constants.USER_ADMIN,   Guid.CreateVersion7(),       true },
        { Constants.USER_ADMIN,   Constants.MEDIA_NATURE_1.Id, false },
        { Constants.USER_JOHNDOE, Constants.MEDIA_NATURE_1.Id, true }
    };

    [Theory]
    [MemberData(nameof(GetGpsData))]
    public async Task GetGps(Guid userId, Guid mediaId, bool nullExpected)
    {
        var repo = GetRepo();

        var gps = await repo.GetGps(userId, mediaId);

        if (nullExpected)
        {
            Assert.Null(gps);
        }
        else
        {
            Assert.NotNull(gps);
        }
    }

    public static TheoryData<Guid, Guid, bool> GetMetadataData => new()
    {
        { Guid.CreateVersion7(),  Guid.CreateVersion7(),       true },
        { Guid.CreateVersion7(),  Constants.MEDIA_NATURE_1.Id, true },
        { Constants.USER_ADMIN,   Guid.CreateVersion7(),       true },
        { Constants.USER_ADMIN,   Constants.MEDIA_NATURE_1.Id, false },
        { Constants.USER_JOHNDOE, Constants.MEDIA_NATURE_1.Id, true }
    };

    [Theory]
    [MemberData(nameof(GetMetadataData))]
    public async Task GetMetadata(Guid userId, Guid mediaId, bool nullExpected)
    {
        var repo = GetRepo();

        var gps = await repo.GetMetadata(userId, mediaId);

        if (nullExpected)
        {
            Assert.Null(gps);
        }
        else
        {
            Assert.NotNull(gps);
            Assert.NotEmpty(gps.RootElement.EnumerateObject());
            Assert.DoesNotContain("SourceFile", gps.RootElement.EnumerateObject().Select(x => x.Name));
            Assert.Contains("EXIF", gps.RootElement.EnumerateObject().Select(x => x.Name));
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
