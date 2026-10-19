using System.IO.Compression;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace MawMedia.Services.Tests;

// the limits on building category archives.  each test has its own asset and
// download directories, and its own writer, so the limits it sets are its own.
public sealed class CategoryZipWriterLimitTests
    : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "maw-media-zip-limits", Guid.CreateVersion7().ToString("N"));
    readonly ManualTime _time = new();

    string AssetDir => Path.Combine(_root, "assets");
    string DownloadDir => Path.Combine(_root, "download");

    public CategoryZipWriterLimitTests()
    {
        Directory.CreateDirectory(AssetDir);
        Directory.CreateDirectory(DownloadDir);
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task ACallerMayOnlyStartSoManyBuildsAnHour()
    {
        var token = TestContext.Current.CancellationToken;
        var writer = Writer(perUserPerHour: 2);
        var user = Guid.CreateVersion7();
        var files = Assets(1, 10);

        Assert.NotNull((await writer.WriteZipFile(user, "a.zip", files, token)).File);
        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.NotNull((await writer.WriteZipFile(user, "b.zip", files, token)).File);

        var refused = await writer.WriteZipFile(user, "c.zip", files, token);

        Assert.Null(refused.File);
        Assert.Equal(ZipBuildRefusal.UserRateLimited, refused.Refusal);
        // until the first of the two leaves the hour
        Assert.Equal(TimeSpan.FromMinutes(50), refused.RetryAfter);
        Assert.False(File.Exists(Path.Combine(DownloadDir, "c.zip")));

        // another caller is not held to this one's count
        Assert.NotNull((await writer.WriteZipFile(Guid.CreateVersion7(), "d.zip", files, token)).File);

        // and the window slides
        _time.Advance(TimeSpan.FromMinutes(50));
        Assert.NotNull((await writer.WriteZipFile(user, "c.zip", files, token)).File);
    }

    [Fact]
    public async Task AnArchiveAlreadyBuiltIsNotCountedAgainstTheCaller()
    {
        var token = TestContext.Current.CancellationToken;
        var writer = Writer(perUserPerHour: 2);
        var user = Guid.CreateVersion7();
        var files = Assets(1, 10);

        await writer.WriteZipFile(user, "a.zip", files, token);

        // a request that lost the race to build it finds it on disk
        for (var i = 0; i < 5; i++)
        {
            Assert.NotNull((await writer.WriteZipFile(user, "a.zip", files, token)).File);
        }

        Assert.NotNull((await writer.WriteZipFile(user, "b.zip", files, token)).File);
    }

    [Fact]
    public async Task NoBuildMayOutgrowTheDownloadDirectory()
    {
        var token = TestContext.Current.CancellationToken;
        var writer = Writer(maxDirectoryMB: 1);
        var user = Guid.CreateVersion7();
        var files = Assets(1, 600 * 1024);

        Assert.NotNull((await writer.WriteZipFile(user, "a.zip", files, token)).File);

        var refused = await writer.WriteZipFile(user, "b.zip", files, token);

        Assert.Equal(ZipBuildRefusal.OutOfSpace, refused.Refusal);
        Assert.True(refused.RetryAfter > TimeSpan.Zero);
        Assert.Equal(["a.zip"], Directory.EnumerateFiles(DownloadDir).Select(Path.GetFileName));

        // room again once the cleaner has been through
        File.Delete(Path.Combine(DownloadDir, "a.zip"));
        Assert.NotNull((await writer.WriteZipFile(user, "b.zip", files, token)).File);
    }

    [Fact]
    public async Task ABuildThatFailsPartWayLeavesNothingToServe()
    {
        var token = TestContext.Current.CancellationToken;
        var writer = Writer();
        var files = Assets(2, 10).Append("/assets/2001/missing/full/gone.avif").ToArray();

        await Assert.ThrowsAnyAsync<IOException>(() => writer.WriteZipFile(Guid.CreateVersion7(), "a.zip", files, token));

        // neither the archive nor its partial: a truncated archive under the cached
        // name would be served as the whole category
        Assert.Empty(Directory.EnumerateFiles(DownloadDir));
        Assert.Null(await writer.GetZipFileIfExists("a.zip"));
    }

    [Fact]
    public async Task OnlySoManyBuildsRunAtOnce()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "holds a build open on a fifo");

        var token = TestContext.Current.CancellationToken;
        var writer = Writer(maxConcurrent: 1);

        // a build whose source is a fifo blocks opening it until something writes,
        // which holds the only build slot for as long as the test wants
        var fifo = Path.Combine(AssetDir, "2001", "fifo", "full", "held.avif");
        Directory.CreateDirectory(Path.GetDirectoryName(fifo)!);
        MakeFifo(fifo);

        var held = Task.Run(() => writer.WriteZipFile(Guid.CreateVersion7(), "held.zip", ["/assets/2001/fifo/full/held.avif"], token), token);

        await Task.Delay(500, token);

        var refused = await writer.WriteZipFile(Guid.CreateVersion7(), "b.zip", Assets(1, 10), token);

        Assert.Equal(ZipBuildRefusal.Busy, refused.Refusal);
        Assert.True(refused.RetryAfter > TimeSpan.Zero);

        // release the held build; its slot comes back
        await File.WriteAllBytesAsync(fifo, [1, 2, 3], token);
        Assert.NotNull((await held).File);
        Assert.NotNull((await writer.WriteZipFile(Guid.CreateVersion7(), "b.zip", Assets(1, 10), token)).File);
    }

    CategoryZipFileWriter Writer(int maxConcurrent = 0, int perUserPerHour = 0, long maxDirectoryMB = 0) =>
        new(
            Options.Create(new AssetConfig { RootDirectory = AssetDir }),
            Options.Create(new CategoryDownloadConfig
            {
                RootDirectory = DownloadDir,
                CleanIntervalInMinutes = 15,
                MaxConcurrentBuilds = maxConcurrent,
                MaxBuildsPerUserPerHour = perUserPerHour,
                MaxDirectorySizeInMB = maxDirectoryMB
            }),
            _time
        );

    // `count` asset files of `bytes` each, returned as the paths the database holds
    string[] Assets(int count, int bytes)
    {
        var tag = Guid.CreateVersion7().ToString("N");

        return [.. Enumerable.Range(0, count).Select(i =>
        {
            var path = $"/assets/2001/{tag}/full/m{i}.avif";
            var file = Path.Combine(AssetDir, AssetPathBuilder.ToRelativeFilePath(path));

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            // random, so the stored (uncompressed) entry is the size of the file
            File.WriteAllBytes(file, [.. Enumerable.Range(0, bytes).Select(_ => (byte)Random.Shared.Next(256))]);

            return path;
        })];
    }

    static void MakeFifo(string path)
    {
        using var mkfifo = System.Diagnostics.Process.Start("mkfifo", [path]);
        mkfifo.WaitForExit();
        Assert.Equal(0, mkfifo.ExitCode);
    }

    sealed class ManualTime : TimeProvider
    {
        DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
