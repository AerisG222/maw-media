namespace MawMedia.Models.FaceRecognition;

// a person as end user applications see them.
//
// deliberately not PersonSync: that carries publisher concerns (source
// revision, status) and, importantly, the *global* face count from
// maw-media-ai.  MediaCount here is scoped to what the caller may see, so it
// cannot be used to infer how much exists behind a permission they lack.
//
// PreferredFaceUrl is absolute so every client does not have to know how to
// assemble it, matching how media file urls are returned.  it is null when the
// person has no preferred face published yet.
public record Person(
    Guid Id,
    string Name,
    string? Slug,
    Guid? PreferredFaceId,
    string? PreferredFaceUrl,
    int MediaCount,
    // per caller, like MediaCount - one user's favourites say nothing about
    // anyone else's
    bool IsFavorite
);
