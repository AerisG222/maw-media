using MawMedia.Services.Abstractions;

namespace MawMedia.Services;

public class AssetPathBuilder
    : IAssetPathBuilder
{
    // media.file.path stores a *url* path - "/assets/2007/hk-macau/qvg-fill/x.avif"
    // - not a filesystem one.  Build below turns it into an absolute url by simple
    // concatenation, which is the whole reason it is stored that way.
    //
    // reading the file instead means undoing that: the asset root on disk *is*
    // what /assets resolves to, so the prefix has to come off before the path is
    // joined to it.  joining it whole yields <root>/assets/2007/... and a
    // FileNotFoundException.
    //
    // a static here rather than a private helper in each caller, because it is the
    // same rule twice - CategoryZipWriter reads assets to build a download, and
    // PlaceCoverStore reads one to publish a cover - and both feed a file read.
    //
    // a path without the prefix is refused rather than assumed relative.  every row
    // has it; one that does not means the storage convention changed, and guessing
    // would read a file from somewhere unintended.
    public static string ToRelativeFilePath(string storedUrlPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedUrlPath);

        if (!storedUrlPath.StartsWith(Constants.AssetBaseUrlWithSlash, StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(storedUrlPath),
                storedUrlPath,
                $"An asset path is expected to begin with '{Constants.AssetBaseUrlWithSlash}'.");
        }

        return storedUrlPath[Constants.AssetBaseUrlWithSlash.Length..];
    }

    public string Build(string baseUrl, string assetPath)
    {
        if (baseUrl.EndsWith('/'))
        {
            baseUrl = baseUrl[..^1];
        }

        return $"{baseUrl}{assetPath}";
    }
}
