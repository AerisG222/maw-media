namespace MawMedia.Services;

public static class CacheKeyBuilder
{
    // incoming path will look like: /assets/2021/category/scale/file.avif
    // this will strip it to the category path so one entry will work for all assets in the category
    public static string CanAccessAsset(Guid userId, string assetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);

        var first = assetPath.LastIndexOf('/');

        if (first == -1)
        {
            throw new ArgumentException($"Invalid asset path detected: {assetPath}");
        }

        var second = assetPath.LastIndexOf('/', first - 1);

        if (second == -1)
        {
            throw new ArgumentException($"Invalid asset path detected: {assetPath}");
        }

        return $"asset-{userId}-{assetPath[..second]}";
    }
}
