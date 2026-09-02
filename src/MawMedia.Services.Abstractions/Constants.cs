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

    // place covers sit under /assets like every other image, but in their own
    // branch, because they are authorized differently from both neighbours.
    //
    // a media asset is checked file by file - media.AllowAccessToAsset asks
    // whether this caller may see this photograph.  a cover deliberately skips
    // that: it has been hand picked by an admin to represent a place, and a place
    // tile has to render for anyone browsing, including callers who cannot reach
    // the category the photograph came from.  the control is in the choosing, not
    // the serving - see media.set_place_cover.
    //
    // what it still requires is a signed in caller holding media:read.  that is
    // the whole difference from the private branch: no per item lookup, but not
    // open to the world either.
    //
    // the media branch has to exclude this prefix by hand, exactly as it already
    // excludes /assets/faces - see StaticFilesExtensions and
    // MediaStaticAssetAuthorizationHandler, which both decline it.
    public const string PlaceCoverBaseUrl = $"{AssetBaseUrl}/covers";
    public const string PlaceCoverBaseUrlWithSlash = $"{PlaceCoverBaseUrl}/";

    // the rendition published as a cover.  'qvg-fill' is 320x240 and cropped to
    // fill, so every tile is the same shape whatever the source photograph's
    // aspect - which is the point of choosing one fixed scale rather than the
    // largest that fits: a grid of covers with mixed aspect ratios looks broken.
    //
    // it is also small, which matters because this branch skips the per file
    // access check: the less a copy carries, the less that decision discloses.
    //
    // a media without this rendition cannot be a cover.  16 of the 167,202 media
    // in the library are in that state, and refusing is better than silently
    // publishing a differently shaped image.
    public const string PlaceCoverScale = "qvg-fill";

    // one format, for the reason the face images give.  the bytes are copied
    // verbatim from an existing avif rendition, so nothing re-encodes and nothing
    // has to record an extension.
    public const string PlaceCoverExtension = ".avif";
    public const string PlaceCoverContentType = "image/avif";

    // {0} is the place id, {1} the cover_created ticks.  the name is derived from
    // the row rather than stored, exactly as a face crop's is; the version lives in
    // the query string so a replaced cover is a new url to a cache while remaining
    // one file on disk.
    public const string PlaceCoverUrlFormat = $"{PlaceCoverBaseUrl}/{{0}}{PlaceCoverExtension}?v={{1}}";
}
