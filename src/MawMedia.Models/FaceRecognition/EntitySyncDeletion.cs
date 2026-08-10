namespace MawMedia.Models.FaceRecognition;

// deletions are explicit: a batch is a delta, so a row simply missing from it
// means "unchanged", not "gone".  they are hard deletes - maw-media-ai is the
// system of record, so a later publish simply recreates the row.
//
// note that an unrecognised entity type now fails deserialization - the whole
// batch is rejected rather than the single row coming back as unknown_entity.
// media.sync_faces keeps that guard anyway, for callers that reach the function
// without passing through this model.
public record EntitySyncDeletion(
    SyncEntityType EntityType,
    Guid Id
);
