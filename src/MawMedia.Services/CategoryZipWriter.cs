
using System.IO.Compression;
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

    // todo: there may be proper async methods in .net10
    public Task<FileInfo> WriteZipFile(string filename, IEnumerable<string> filePaths)
    {
        var archivePath = BuildDownloadFilePath(filename);

        using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create);

        foreach (var path in filePaths)
        {
            zip.CreateEntryFromFile(
                Path.Combine(_assetRootDir, TrimAssetsPathPrefix(path)),
                Path.GetFileName(path),
                CompressionLevel.NoCompression  // media assets already compressed so don't waste cpu
            );
        }

        return Task.FromResult(new FileInfo(archivePath));
    }

    string BuildDownloadFilePath(string filename)
    {
        return Path.Combine(_downloadRootDir, filename);
    }

    string TrimAssetsPathPrefix(string urlPath)
    {
        if (!urlPath.StartsWith("/assets/"))
        {
            throw new ArgumentOutOfRangeException(nameof(urlPath));
        }

        // todo: TEMP - remove once media migrated
        urlPath = urlPath
            .Replace("-", "_")
            .Replace("/qvg/", "/lg/")
            .Replace(".avif", ".jpg");
        // TEMP END

        return urlPath["/assets/".Length..];
    }
}
