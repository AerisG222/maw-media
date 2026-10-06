using MawMedia.Services.Models;

namespace MawMedia.Services.Tests;

// the asset access cache, which answers "may this caller fetch this file" for 30
// seconds after the database last did.
//
// its key is what this file is about.  it used to be the category's directory,
// so one answer stood in for every file in a category - correct while access was
// decided per category, and a leak once a media can be restricted to fewer roles
// than its category grants: a caller who opened any visible photo could then
// fetch every file beside it.  these tests pin the key to the file.
public class AssetAccessCacheTests
{
    static readonly Guid USER = Guid.CreateVersion7();

    [Fact]
    public void FilesSharingADirectoryDoNotShareAnEntry()
    {
        // two media in the same category, same scale - the case the old key
        // collapsed into one
        Assert.NotEqual(
            CacheKeyBuilder.CanAccessAsset(USER, "/assets/2022/nature/qvg/visible.avif"),
            CacheKeyBuilder.CanAccessAsset(USER, "/assets/2022/nature/qvg/restricted.avif"));
    }

    [Fact]
    public void RenditionsOfOneMediaDoNotShareAnEntry()
    {
        // per file rather than per media: nothing in a path names its media, and
        // the obvious stand in - the name without its scale directory - is shared
        // by unrelated media such as the two halves of a live photo
        Assert.NotEqual(
            CacheKeyBuilder.CanAccessAsset(USER, "/assets/2022/nature/qvg/a.avif"),
            CacheKeyBuilder.CanAccessAsset(USER, "/assets/2022/nature/full-hd/a.avif"));
    }

    [Fact]
    public void TheSameFileForTheSameCallerIsOneEntry()
    {
        Assert.Equal(
            CacheKeyBuilder.CanAccessAsset(USER, "/assets/2022/nature/qvg/a.avif"),
            CacheKeyBuilder.CanAccessAsset(USER, "/assets/2022/nature/qvg/a.avif"));
    }

    [Fact]
    public void CallersDoNotShareAnEntry()
    {
        Assert.NotEqual(
            CacheKeyBuilder.CanAccessAsset(Guid.CreateVersion7(), "/assets/2022/nature/qvg/a.avif"),
            CacheKeyBuilder.CanAccessAsset(Guid.CreateVersion7(), "/assets/2022/nature/qvg/a.avif"));
    }

    [Fact]
    public async Task ListingPrimesEveryFileItReturned()
    {
        var cache = new FakeHybridCache();
        var mediaId = Guid.CreateVersion7();
        var paths = new[]
        {
            "/assets/2022/nature/qqvg/a.avif",
            "/assets/2022/nature/qvg/a.avif",
            "/assets/2022/nature/full-hd/a.avif"
        };

        await BaseRepository.AssembleMedia(
            USER,
            [.. paths.Select(p => Row(mediaId, p))],
            "https://example.test",
            new AssetPathBuilder(),
            cache,
            TestContext.Current.CancellationToken);

        // priming only the first, as before, would leave the other renditions to a
        // database lookup each - correct, but the priming would have stopped
        // doing its job without anything failing
        Assert.Equal(
            paths.Select(p => CacheKeyBuilder.CanAccessAsset(USER, p)).Order(),
            cache.SetKeys.Order());
    }

    [Fact]
    public async Task ListingPrimesNothingItDidNotReturn()
    {
        var cache = new FakeHybridCache();

        await BaseRepository.AssembleMedia(
            USER,
            [Row(Guid.CreateVersion7(), "/assets/2022/nature/qvg/visible.avif")],
            "https://example.test",
            new AssetPathBuilder(),
            cache,
            TestContext.Current.CancellationToken);

        // the sibling the listing withheld must not be granted by the one it showed
        Assert.DoesNotContain(
            CacheKeyBuilder.CanAccessAsset(USER, "/assets/2022/nature/qvg/restricted.avif"),
            cache.SetKeys);
    }

    static MediaAndFile Row(Guid mediaId, string path) =>
        new()
        {
            MediaId = mediaId,
            MediaSlug = "a",
            CategoryId = Guid.Empty,
            CategoryYear = 2022,
            CategorySlug = "nature",
            MediaType = "photo",
            FileId = Guid.CreateVersion7(),
            FilePath = path,
            FileType = "avif",
            FileScale = "qvg"
        };
}
