namespace MawMedia.Services.Models;

// the shape media.get_persons returns.  distinct from MawMedia.Models
// .FaceRecognition.Person because the url on that model is composed from the
// request's base url, which sql has no notion of - the repository maps between
// the two.
record PersonRow(
    Guid Id,
    string Name,
    string? Slug,
    Guid? PreferredFaceId,
    int MediaCount,
    bool IsFavorite
);
