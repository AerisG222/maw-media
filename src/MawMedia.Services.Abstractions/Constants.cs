namespace MawMedia.Services.Abstractions;

public static class Constants
{
    public const string AssetBaseUrl = "/assets";
    public const string AssetBaseUrlWithSlash = $"{AssetBaseUrl}/";

    // the api route serving a published face crop.  the version is part of the
    // route table (see ApiVersioningExtensions), so introducing v2 means
    // revisiting this - PersonRoutesTests asserts the shape so the drift is
    // caught rather than shipped.
    public const string FaceImageUrlFormat = "/api/v1/faces/{0}/image";
}
