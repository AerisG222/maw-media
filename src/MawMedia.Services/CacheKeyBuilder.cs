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

    // deliberately NOT routed through CanAccessAsset.  that helper strips a path
    // to its second to last segment so one entry covers a whole category
    // directory, and every face image lives in the same flat directory - it
    // would collapse to a single key and let the first answer stand in for every
    // face.  face visibility is per face, so the key is too.
    public static string CanViewFace(Guid userId, Guid faceId) => $"face-{userId}-{faceId}";

    public static string UserState(string externalId) => $"user-state-{externalId}";
}
