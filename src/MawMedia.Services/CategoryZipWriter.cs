using System.Collections.Concurrent;
using System.IO.Compression;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace MawMedia.Services;

// builds the category archives, within limits.
//
// an archive costs real disk and io - the median category is ~40MB, the largest
// ~10GB - and every distinct set of files a caller may see is a distinct archive
// kept for hours.  so a build, unlike serving an archive already on disk, is
// bounded three ways, each configurable in CategoryDownloadConfig (0 turns a
// limit off):
//
//   MaxConcurrentBuilds      builds running at once across all callers
//   MaxBuildsPerUserPerHour  builds one caller may start in a sliding hour
//   MaxDirectorySizeInMB     what the download directory may hold, counting the
//                            builds in flight at their full size
//
// a build refused by any of them is reported with how long to wait, rather than
// queued: the caller is a browser waiting on a response.
public class CategoryZipFileWriter
    : IZipFileWriter
{
    const int WAIT_TIMEOUT_MS = 5_000;
    const string PARTIAL_EXTENSION = ".partial";
    static readonly TimeSpan RATE_WINDOW = TimeSpan.FromHours(1);
    static readonly TimeSpan BUSY_RETRY_AFTER = TimeSpan.FromSeconds(30);

    readonly string _assetRootDir;
    readonly string _downloadRootDir;
    readonly CategoryDownloadConfig _config;
    readonly TimeProvider _time;
    readonly ConcurrentDictionary<string, SemaphoreSlim> _buildLocks = new();
    readonly SemaphoreSlim? _buildSlots;
    readonly Dictionary<Guid, Queue<DateTimeOffset>> _recentBuilds = [];
    readonly Lock _spaceLock = new();
    long _reservedBytes;

    public CategoryZipFileWriter(
        IOptions<AssetConfig> assetOpts,
        IOptions<CategoryDownloadConfig> downloadOpts,
        TimeProvider? timeProvider = null
    )
    {
        ArgumentNullException.ThrowIfNull(assetOpts);
        ArgumentNullException.ThrowIfNull(downloadOpts);

        _assetRootDir = assetOpts.Value.RootDirectory;
        _downloadRootDir = downloadOpts.Value.RootDirectory;
        _config = downloadOpts.Value;
        _time = timeProvider ?? TimeProvider.System;

        if (_config.MaxConcurrentBuilds > 0)
        {
            _buildSlots = new SemaphoreSlim(_config.MaxConcurrentBuilds, _config.MaxConcurrentBuilds);
        }
    }

    public Task<FileInfo?> GetZipFileIfExists(string filename)
    {
        var fi = new FileInfo(BuildDownloadFilePath(filename));

        if (fi.Exists)
        {
            return Task.FromResult((FileInfo?)fi);
        }

        return Task.FromResult((FileInfo?)null);
    }

    public async Task<ZipBuildResult> WriteZipFile(Guid userId, string filename, IEnumerable<string> filePaths, CancellationToken token = default)
    {
        var gate = _buildLocks.GetOrAdd(filename, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(token);

        try
        {
            var archivePath = BuildDownloadFilePath(filename);

            // built by a request that held the gate before this one - free, like
            // any other archive already on disk
            if (File.Exists(archivePath))
            {
                return ZipBuildResult.Built(new FileInfo(archivePath));
            }

            if (_buildSlots != null && !await _buildSlots.WaitAsync(WAIT_TIMEOUT_MS, token))
            {
                return ZipBuildResult.Refused(ZipBuildRefusal.Busy, BUSY_RETRY_AFTER);
            }

            try
            {
                var sources = filePaths
                    .Select(p => (Source: Path.Combine(_assetRootDir, AssetPathBuilder.ToRelativeFilePath(p)), Name: Path.GetFileName(p)))
                    .ToArray();
                var bytes = _config.MaxDirectorySizeInMB > 0
                    ? sources.Sum(s => new FileInfo(s.Source).Length)
                    : 0;

                // space before the rate limit, so a build refused for space does not
                // also spend one of the caller's builds
                if (!TryReserveSpace(bytes))
                {
                    return ZipBuildResult.Refused(ZipBuildRefusal.OutOfSpace, TimeSpan.FromMinutes(Math.Max(1, _config.CleanIntervalInMinutes)));
                }

                try
                {
                    if (!TryStartUserBuild(userId, out var retryAfter))
                    {
                        return ZipBuildResult.Refused(ZipBuildRefusal.UserRateLimited, retryAfter);
                    }

                    await Build(archivePath, sources, token);

                    return ZipBuildResult.Built(new FileInfo(archivePath));
                }
                finally
                {
                    ReleaseSpace(bytes);
                }
            }
            finally
            {
                _buildSlots?.Release();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    // written under a temporary name and moved into place only once complete.
    // the archive's name is what the cache trusts, so a build cut short - the
    // caller going away, a full disk - must never leave a truncated file under it
    // to be served as the whole category
    static async Task Build(string archivePath, (string Source, string Name)[] sources, CancellationToken token)
    {
        var partialPath = $"{archivePath}.{Guid.CreateVersion7():N}{PARTIAL_EXTENSION}";

        try
        {
            await using (var zip = await ZipFile.OpenAsync(partialPath, ZipArchiveMode.Create, token))
            {
                foreach (var (source, name) in sources)
                {
                    await zip.CreateEntryFromFileAsync(
                        source,
                        name,
                        CompressionLevel.NoCompression,  // media assets already compressed so don't waste cpu
                        token
                    );
                }
            }

            File.Move(partialPath, archivePath);
        }
        catch
        {
            File.Delete(partialPath);
            throw;
        }
    }

    bool TryStartUserBuild(Guid userId, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        if (_config.MaxBuildsPerUserPerHour <= 0)
        {
            return true;
        }

        var now = _time.GetUtcNow();

        lock (_recentBuilds)
        {
            if (!_recentBuilds.TryGetValue(userId, out var starts))
            {
                starts = new Queue<DateTimeOffset>();
                _recentBuilds[userId] = starts;
            }

            while (starts.Count > 0 && starts.Peek() <= now - RATE_WINDOW)
            {
                starts.Dequeue();
            }

            if (starts.Count >= _config.MaxBuildsPerUserPerHour)
            {
                retryAfter = starts.Peek() + RATE_WINDOW - now;
                return false;
            }

            starts.Enqueue(now);
            return true;
        }
    }

    bool TryReserveSpace(long bytes)
    {
        if (_config.MaxDirectorySizeInMB <= 0)
        {
            return true;
        }

        var limit = _config.MaxDirectorySizeInMB * 1024 * 1024;

        lock (_spaceLock)
        {
            // partial files are the builds in flight, already counted whole in
            // _reservedBytes
            var onDisk = new DirectoryInfo(_downloadRootDir)
                .EnumerateFiles()
                .Where(f => !f.Name.EndsWith(PARTIAL_EXTENSION, StringComparison.Ordinal))
                .Sum(f => f.Length);

            if (onDisk + _reservedBytes + bytes > limit)
            {
                return false;
            }

            _reservedBytes += bytes;
            return true;
        }
    }

    void ReleaseSpace(long bytes)
    {
        if (_config.MaxDirectorySizeInMB <= 0)
        {
            return;
        }

        lock (_spaceLock)
        {
            _reservedBytes -= bytes;
        }
    }

    string BuildDownloadFilePath(string filename)
    {
        return Path.Combine(_downloadRootDir, filename);
    }
}
