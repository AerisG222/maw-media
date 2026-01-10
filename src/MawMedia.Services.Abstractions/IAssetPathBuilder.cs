namespace MawMedia.Services.Abstractions;

public interface IAssetPathBuilder
{
    string Build(string baseUrl, string assetPathRelativeUrl);
}
