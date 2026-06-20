
using System.Collections.Concurrent;
using System.IO.Compression;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace MawMedia.Services;

public class CategoryZipFileWriter
    : IZipFileWriter
{
    const int WAIT_TIMEOUT_MS = 5_000;

    readonly string _assetRootDir;
    readonly string _downloadRootDir;
    readonly ConcurrentDictionary<string, SemaphoreSlim> _buildLocks = new();

    public CategoryZipFileWriter(
        IOptions<AssetConfig> assetOpts,
        IOptions<CategoryDownloadConfig> downloadOpts
    )
    {
        ArgumentNullException.ThrowIfNull(assetOpts);
        ArgumentNullException.ThrowIfNull(downloadOpts);

        _assetRootDir = assetOpts.Value.RootDirectory;
        _downloadRootDir = downloadOpts.Value.RootDirectory;
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

    public async Task<FileInfo> WriteZipFile(string filename, IEnumerable<string> filePaths, CancellationToken token = default)
    {
        var gate = _buildLocks.GetOrAdd(filename, _ => new SemaphoreSlim(1, 1));

        try
        {
            await gate.WaitAsync(token);

            var archivePath = BuildDownloadFilePath(filename);

            if (File.Exists(archivePath))
            {
                return new FileInfo(archivePath);
            }

            using var zip = await ZipFile.OpenAsync(archivePath, ZipArchiveMode.Create);

            foreach (var path in filePaths)
            {
                await zip.CreateEntryFromFileAsync(
                    Path.Combine(_assetRootDir, TrimAssetsPathPrefix(path)),
                    Path.GetFileName(path),
                    CompressionLevel.NoCompression  // media assets already compressed so don't waste cpu
                );
            }

            return new FileInfo(archivePath);
        }
        finally
        {
            gate?.Release();
        }
    }

    string BuildDownloadFilePath(string filename)
    {
        return Path.Combine(_downloadRootDir, filename);
    }

    static string TrimAssetsPathPrefix(string urlPath)
    {
        if (!urlPath.StartsWith(Constants.AssetBaseUrlWithSlash))
        {
            throw new ArgumentOutOfRangeException(nameof(urlPath));
        }

        return urlPath[Constants.AssetBaseUrlWithSlash.Length..];
    }
}
