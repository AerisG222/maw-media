using MawMedia.Models.FaceRecognition;

namespace MawMedia.Services.Abstractions;

// each call is one transaction and one collection.  the publisher is
// responsible for ordering them: statuses before persons (status_code is a
// foreign key), persons before faces (person_id is a foreign key).  deletions
// may run at any point.
public interface IFaceRepository
{
    Task<IEnumerable<FaceSyncResult>> SyncPersonStatuses(Guid userId, IEnumerable<PersonStatusSync> statuses, CancellationToken token = default);
    Task<IEnumerable<FaceSyncResult>> SyncPersons(Guid userId, IEnumerable<PersonSync> persons, CancellationToken token = default);
    Task<IEnumerable<FaceSyncResult>> SyncFaces(Guid userId, IEnumerable<FaceSync> faces, CancellationToken token = default);
    Task<IEnumerable<FaceSyncResult>> SyncDeletions(Guid userId, IEnumerable<EntitySyncDeletion> deletions, CancellationToken token = default);
}
