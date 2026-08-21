namespace MawMedia.Services.Abstractions;

public static class Constants
{
    public const string AssetBaseUrl = "/assets";
    public const string AssetBaseUrlWithSlash = $"{AssetBaseUrl}/";

    // face crops are served as static files rather than through an api route.
    // they are content addressed by face id and immutable once published, and
    // the person picker asks for a few hundred at once - static file middleware
    // answers a conditional GET with 304, which the api route could not without
    // hand rolling an etag.  the url also stops carrying an api version, which
    // an image has no business being tied to.
    public const string FaceAssetBaseUrl = $"{AssetBaseUrl}/faces";
    public const string FaceAssetBaseUrlWithSlash = $"{FaceAssetBaseUrl}/";

    // a single format keeps the path derivable from the face id alone - no
    // database lookup and no directory probe to answer "where is this image".
    // see FaceImageStore, which owns the storage side of the same decision.
    public const string FaceImageExtension = ".avif";
    public const string FaceImageContentType = "image/avif";

    // FaceRepository composes the absolute url from this; PersonRoutesTests
    // asserts the shape so a change here cannot ship unnoticed.
    public const string FaceImageUrlFormat = $"{FaceAssetBaseUrl}/{{0}}{FaceImageExtension}";
}
