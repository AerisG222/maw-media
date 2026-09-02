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

    // the admin/1 case is a range rather than an exact count on purpose.  admin can see
    // four media but only three have files, because MEDIA_FOOD_1 is deliberately seeded
    // without any (see Constants).  get_random_media picks _count media at random and
    // then inner joins to media_detail, so a request for one media returns nothing
    // whenever that random pick lands on the file-less one.
    public static TheoryData<Guid, byte, int, int> GetRandomMediaData => new()
    {
        //                        count  min  max
        { Guid.CreateVersion7(),  10,    0,   0 },
        { Constants.USER_ADMIN,   1,     0,   1 },
        // admin reaches six media carrying files: the two nature, travel, and the
        // three place fixtures.  johndoe reaches only CATEGORY_TRAVEL, which now
        // holds travel plus the two Boston place fixtures.
        { Constants.USER_ADMIN,   10,    6,   6 },
        { Constants.USER_JOHNDOE, 1,     0,   1 },
        { Constants.USER_JOHNDOE, 200,   3,   3 }
    };

    [Theory]
    [MemberData(nameof(GetRandomMediaData))]
    public async Task GetRandomMedia(Guid userId, byte count, int minExpected, int maxExpected)
    {
        var repo = GetRepo();

        var media = await repo.GetRandomMedia(userId, "http://example.com", count, TestContext.Current.CancellationToken);

        Assert.NotNull(media);
        Assert.InRange(media.Count(), minExpected, maxExpected);
    }

    public static TheoryData<Guid, Guid, DbMedia?, int> GetMediaData => new()
    {
        { Guid.CreateVersion7(),  Guid.CreateVersion7(),       null, 0 },
        { Guid.CreateVersion7(),  Constants.MEDIA_NATURE_1.Id, null, 0 },
        { Constants.USER_ADMIN,   Guid.CreateVersion7(),       null, 0 },
        { Constants.USER_ADMIN,   Constants.MEDIA_NATURE_1.Id, Constants.MEDIA_NATURE_1, 2 },  // full-hd plus the qvg-fill a cover is published from
        { Constants.USER_JOHNDOE, Constants.MEDIA_NATURE_1.Id, null, 0 }
    };

    [Theory]
    [MemberData(nameof(GetMediaData))]
    public async Task GetMedia(Guid userId, Guid mediaId, DbMedia? expectedMedia, int expectedFileCount)
    {
        var repo = GetRepo();

        var media = await repo.GetMedia(userId, "http://example.com", mediaId, TestContext.Current.CancellationToken);

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

        var gps = await repo.GetGps(userId, mediaId, TestContext.Current.CancellationToken);

        if (nullExpected)
        {
            Assert.Null(gps);
        }
        else
        {
            Assert.NotNull(gps);
            Assert.Equal(gps.Recorded?.Latitude, Constants.LOCATION_NY.Latitude);
            Assert.Equal(gps.Recorded?.Longitude, Constants.LOCATION_NY.Longitude);
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

        var gps = await repo.GetMetadata(userId, mediaId, TestContext.Current.CancellationToken);

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

        var updatedMedia = await repo.SetIsFavorite(userId, "http://example.com", mediaId, doFavorite, TestContext.Current.CancellationToken);

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

        var comments = await repo.GetComments(userId, mediaId, TestContext.Current.CancellationToken);

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

        var newId = await repo.AddComment(userId, mediaId, "test comment", TestContext.Current.CancellationToken);

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

        var file = await repo.GetMediaFile(userId, assetId, TestContext.Current.CancellationToken);

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
        { Guid.CreateVersion7(),  $"{Abstractions.Constants.AssetBaseUrl}/b/c.avif",           null },
        { Guid.CreateVersion7(),  Constants.FILE_NATURE_2.Path, null },
        { Constants.USER_ADMIN,   $"{Abstractions.Constants.AssetBaseUrl}/b/c.avif",           null },
        { Constants.USER_JOHNDOE, Constants.FILE_NATURE_2.Path, null },
        { Constants.USER_ADMIN,   Constants.FILE_NATURE_2.Path, Constants.FILE_NATURE_2 }
    };

    [Theory]
    [MemberData(nameof(GetMediaFileByPathData))]
    public async Task GetMediaFileByPath(Guid userId, string path, DbFile? expected)
    {
        var repo = GetRepo();

        var file = await repo.GetMediaFile(userId, path, TestContext.Current.CancellationToken);

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

        var token = TestContext.Current.CancellationToken;
        var result = await repo.SetGpsOverride(userId, mediaId, newLocationId, latitude, longitude, token);

        Assert.Equal(expectSuccess, result);

        if (expectSuccess)
        {
            var loc = await repo.GetGps(userId, mediaId, token);
            Assert.Equal(latitude, loc?.Override?.Latitude);
            Assert.Equal(longitude, loc?.Override?.Longitude);
        }
    }

    MediaRepository GetRepo()
    {
        return new MediaRepository(
            new FakeLogger<MediaRepository>(),
            _fixture.DataSource.CreateConnection(),
            new FakeHybridCache(),
            new AssetPathBuilder()
        );
    }
}
