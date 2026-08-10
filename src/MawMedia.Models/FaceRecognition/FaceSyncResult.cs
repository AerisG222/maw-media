namespace MawMedia.Models.FaceRecognition;

// per-item result, so the publisher can act on partial success.
//
// Outcome is one of:
//   applied         - inserted or updated
//   skipped_stale   - SourceRevision was not newer than what is stored
//   unresolved_path - no media.file row matched the supplied path
//   deleted         - soft deleted
//   not_found       - a deletion named a row that is not here
//   unknown_entity  - a deletion named something other than person or face
//   forbidden       - the caller is not an admin; nothing in the batch applied
//
// Detail carries the offending path for unresolved_path and the code for a
// status, and is null otherwise.
public record FaceSyncResult(
    string Entity,
    Guid? EntityId,
    string Outcome,
    string? Detail
);
