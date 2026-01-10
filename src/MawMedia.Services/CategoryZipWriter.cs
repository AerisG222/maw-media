
using System.IO.Compression;
using MawMedia.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace MawMedia.Services;

public class CategoryZipFileWriter
    : IZipFileWriter
{
    readonly string _assetRootDir;
    readonly string _downloadRootDir;

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

    public async Task<FileInfo> WriteZipFile(string filename, IEnumerable<string> filePaths)
    {
        var archivePath = BuildDownloadFilePath(filename);

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
