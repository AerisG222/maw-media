namespace MawMedia.Services;

public interface IAssetPathBuilder
{
    string Build(string baseUrl, string assetPathRelativeUrl);
}
