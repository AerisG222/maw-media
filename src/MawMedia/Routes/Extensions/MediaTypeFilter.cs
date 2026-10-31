namespace MawMedia.Routes.Extensions;

// the ?t= a listing can be narrowed by.  checked rather than passed along, so a
// typo is a 400 instead of an empty page that looks like there is nothing there.
public static class MediaTypeFilter
{
    public const string INVALID_MESSAGE = "Media type must be photo or video.";

    public static bool IsValid(string? mediaType) =>
        mediaType is null or "photo" or "video";
}
