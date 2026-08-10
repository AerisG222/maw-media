using NodaTime;

namespace MawMedia.Models.FaceRecognition;

// an upsert replaces the whole row, so every field must be supplied on every
// publish.  omitting one clears it rather than leaving it untouched.
//
// SourceRevision drives the staleness guard: a batch whose revision is not
// newer than what is stored is skipped, which is what makes the publisher's
// at-least-once delivery safe to retry.
public record PersonSync(
    Guid Id,
    string? Name,
    string? Slug,
    string? StatusCode,
    Guid? PreferredFaceId,
    int FaceCount,
    long SourceRevision,
    Instant? SourceModified
);
