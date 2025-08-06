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

        var media = await repo.GetRandomMedia(userId, "http://example.com", count);

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

        var media = await repo.GetMedia(userId, "http://example.com", mediaId);

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
            Assert.Equal(gps.Latitude, Constants.LOCATION_NY.Latitude);
            Assert.Equal(gps.Longitude, Constants.LOCATION_NY.Longitude);
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

    public static TheoryData<Guid, Guid, bool, bool> FavoriteMediaData => new()
    {
        { Guid.CreateVersion7(), Guid.CreateVersion7(),       true,  true  },
        { Guid.CreateVersion7(), Guid.CreateVersion7(),       true,  false },
        { Guid.CreateVersion7(), Constants.MEDIA_NATURE_1.Id, true,  true  },
        { Guid.CreateVersion7(), Constants.MEDIA_NATURE_1.Id, true,  false },
        { Constants.USER_ADMIN,  Guid.CreateVersion7(),       true,  true  },
        { Constants.USER_ADMIN,  Guid.CreateVersion7(),       true,  false },
        { Constants.USER_ADMIN,  Constants.MEDIA_NATURE_1.Id, false, true  },
        { Constants.USER_ADMIN,  Constants.MEDIA_NATURE_1.Id, false, false }
    };

    [Theory]
    [MemberData(nameof(FavoriteMediaData))]
    public async Task FavoriteMedia(Guid userId, Guid mediaId, bool shouldReturnNull, bool doFavorite)
    {
        var repo = GetRepo();

        var updatedMedia = await repo.SetIsFavorite(userId, "http://example.com", mediaId, doFavorite);

        if (shouldReturnNull)
        {
            Assert.Null(updatedMedia);
        }
        else
        {
            Assert.NotNull(updatedMedia);
            Assert.Equal(Constants.MEDIA_NATURE_1.Id, updatedMedia.Id);
            Assert.Equal(doFavorite, updatedMedia.IsFavorite);
        }
    }

    public static TheoryData<Guid, Guid, int> GetCommentsData => new()
    {
        { Guid.CreateVersion7(), Guid.CreateVersion7(),         0 },
        { Guid.CreateVersion7(), Constants.MEDIA_NATURE_1.Id,   0 },
        { Constants.USER_ADMIN,  Guid.CreateVersion7(),         0 },
        { Constants.USER_ADMIN,  Constants.MEDIA_NATURE_1.Id,   1 },
        { Constants.USER_JOHNDOE,  Constants.MEDIA_NATURE_1.Id, 0 }
    };

    [Theory]
    [MemberData(nameof(GetCommentsData))]
    public async Task GetComments(Guid userId, Guid mediaId, int expectedCount)
    {
        var repo = GetRepo();

        var comments = await repo.GetComments(userId, mediaId);

        if (expectedCount == 0)
        {
            Assert.Empty(comments);
        }
        else
        {
            Assert.NotEmpty(comments);
            Assert.Equal(expectedCount, comments.Count());
        }
    }

    public static TheoryData<Guid, Guid, bool> AddCommentsData => new()
    {
        { Guid.CreateVersion7(), Guid.CreateVersion7(),         false },
        { Guid.CreateVersion7(), Constants.MEDIA_NATURE_2.Id,   false },
        { Constants.USER_ADMIN,  Guid.CreateVersion7(),         false },
        { Constants.USER_JOHNDOE,  Constants.MEDIA_NATURE_2.Id, false },
        { Constants.USER_ADMIN,  Constants.MEDIA_NATURE_2.Id,   true }
    };

    [Theory]
    [MemberData(nameof(AddCommentsData))]
    public async Task AddComments(Guid userId, Guid mediaId, bool shouldAdd)
    {
        var repo = GetRepo();

        var newId = await repo.AddComment(userId, mediaId, "test comment");

        if (shouldAdd)
        {
            Assert.NotNull(newId);
        }
        else
        {
            Assert.Null(newId);
        }
    }

    public static TheoryData<Guid, Guid, DbFile?> GetMediaFileByIdData => new()
    {
        { Guid.CreateVersion7(),  Guid.CreateVersion7(),      null },
        { Guid.CreateVersion7(),  Constants.FILE_NATURE_2.Id, null },
        { Constants.USER_ADMIN,   Guid.CreateVersion7(),      null },
        { Constants.USER_JOHNDOE, Constants.FILE_NATURE_2.Id, null },
        { Constants.USER_ADMIN,   Constants.FILE_NATURE_2.Id, Constants.FILE_NATURE_2 }
    };

    [Theory]
    [MemberData(nameof(GetMediaFileByIdData))]
    public async Task GetMediaFileById(Guid userId, Guid assetId, DbFile? expected)
    {
        var repo = GetRepo();

        var file = await repo.GetMediaFile(userId, assetId);

        if (expected == null)
        {
            Assert.Null(file);
        }
        else
        {
            Assert.NotNull(file);
            Assert.Equal(expected.Id, file.Id);
        }
    }

    public static TheoryData<Guid, string, DbFile?> GetMediaFileByPathData => new()
    {
        { Guid.CreateVersion7(),  $"{Services.Constants.AssetBaseUrl}/b/c.avif",           null },
        { Guid.CreateVersion7(),  Constants.FILE_NATURE_2.Path, null },
        { Constants.USER_ADMIN,   $"{Services.Constants.AssetBaseUrl}/b/c.avif",           null },
        { Constants.USER_JOHNDOE, Constants.FILE_NATURE_2.Path, null },
        { Constants.USER_ADMIN,   Constants.FILE_NATURE_2.Path, Constants.FILE_NATURE_2 }
    };

    [Theory]
    [MemberData(nameof(GetMediaFileByPathData))]
    public async Task GetMediaFileByPath(Guid userId, string path, DbFile? expected)
    {
        var repo = GetRepo();

        var file = await repo.GetMediaFile(userId, path);

        if (expected == null)
        {
            Assert.Null(file);
        }
        else
        {
            Assert.NotNull(file);
            Assert.Equal(expected.Id, file.Id);
        }
    }

    public static TheoryData<Guid, Guid, Guid, decimal, decimal, bool> SetGpsOverrideData => new()
    {
        { Guid.CreateVersion7(),  Guid.CreateVersion7(),       Guid.CreateVersion7(),    1, 2, false },
        { Guid.CreateVersion7(),  Constants.MEDIA_NATURE_2.Id, Guid.CreateVersion7(),    1, 2, false },
        { Constants.USER_ADMIN,   Guid.CreateVersion7(),       Guid.CreateVersion7(),    1, 2, false },
        { Constants.USER_JOHNDOE, Constants.MEDIA_NATURE_2.Id, Guid.CreateVersion7(),    1, 2, false },
        { Constants.USER_ADMIN,   Constants.MEDIA_NATURE_2.Id, Constants.LOCATION_MA.Id, 1, 2, true }
    };

    [Theory]
    [MemberData(nameof(SetGpsOverrideData))]
    public async Task SetGpsOverride(Guid userId, Guid mediaId, Guid newLocationId, decimal latitude, decimal longitude, bool expectSuccess)
    {
        var repo = GetRepo();

        var result = await repo.SetGpsOverride(userId, mediaId, newLocationId, latitude, longitude);

        Assert.Equal(expectSuccess, result);

        if (expectSuccess)
        {
            var loc = await repo.GetGps(userId, mediaId);
            Assert.Equal(latitude, loc?.Latitude);
            Assert.Equal(longitude, loc?.Longitude);
        }
    }

    MediaRepository GetRepo()
    {
        return new MediaRepository(
            new FakeLogger<MediaRepository>(),
            _fixture.DataSource.CreateConnection(),
            new AssetPathBuilder()
        );
    }
}
