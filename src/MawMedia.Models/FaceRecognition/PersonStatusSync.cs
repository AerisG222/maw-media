namespace MawMedia.Models.FaceRecognition;

// a triage state for clusters that are deliberately not named.  maw-media-ai
// owns these codes, so media.person_status is not seeded here - the codes
// arrive with a publish and must be applied before the persons using them.
public record PersonStatusSync(
    string Code,
    string Label,
    string? Description,
    int SortOrder
);
