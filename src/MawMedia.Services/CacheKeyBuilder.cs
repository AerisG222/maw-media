namespace MawMedia.Services;

public static class CacheKeyBuilder
{
    // one entry per file, keyed on the full path.
    //
    // 2026-10-06 - this used to strip a path such as
    // /assets/2021/category/scale/file.avif back to /assets/2021/category, so one
    // entry answered for every file in a category's directory.  that was correct
    // while access was decided per category, and it stopped being correct when a
    // media could be restricted to fewer roles than its category grants: a caller
    // who opened any visible photo would have been handed every file beside it,
    // restricted ones included, for as long as the entry lived - and the paths are
    // predictable enough to ask for.
    //
    // per file rather than per media, though media is the level access is now
    // decided at, because nothing in a path names its media.  the obvious stand in
    // - the file name with the scale directory stripped - is not unique: a
    // photograph and a video shot as one live photo share a name, and so would
    // share an entry.  the full path cannot collide, and a miss costs one
    // indexed lookup in media.get_media_file.
    public static string CanAccessAsset(Guid userId, string assetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);

        return $"asset-{userId}-{assetPath}";
    }

    // per face for the same reason CanAccessAsset is per file: visibility is
    // decided below the level a shared key would cover.  every face image lives in
    // one flat directory, so a key built from the directory would have let the
    // first answer stand in for every face.
    public static string CanViewFace(Guid userId, Guid faceId) => $"face-{userId}-{faceId}";

    public static string UserState(string externalId) => $"user-state-{externalId}";
}
