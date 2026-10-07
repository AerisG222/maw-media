namespace MawMedia.Models;

// one restricted media, as the admin listing of restrictions returns it.
//
// metadata only, with no files: the admin asking may not be allowed to fetch the
// files of a media hidden from them, and a listing that handed out paths /assets
// would then refuse is worse than one that does not.  IsVisibleToYou says which
// these are - the photos an admin has restricted to roles they do not hold, which
// drop out of every other listing they can make.  see docs/media-restrictions.md
public record RestrictedMedia(
    Guid MediaId,
    string MediaSlug,
    string MediaType,
    Guid CategoryId,
    string CategoryName,
    short CategoryYear,
    string CategorySlug,
    IReadOnlyList<string> Roles,
    bool IsVisibleToYou
);
