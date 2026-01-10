using MawMedia.Services.Abstractions;

namespace MawMedia.Services;

public class AssetPathBuilder
    : IAssetPathBuilder
{
    public string Build(string baseUrl, string assetPath)
    {
        if (baseUrl.EndsWith('/'))
        {
            baseUrl = baseUrl[..^1];
        }

        return $"{baseUrl}{assetPath}";
    }
}
