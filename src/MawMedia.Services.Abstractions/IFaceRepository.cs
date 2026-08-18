using MawMedia.Models.FaceRecognition;

namespace MawMedia.Services.Abstractions;

// each call is one transaction and one collection.  the publisher is
// responsible for ordering them: statuses before persons (status_code is a
// foreign key), persons before faces (person_id is a foreign key).  the two
// deletes may run at any point and in either order - ON DELETE SET NULL on
// media.face.person_id means dropping a person only unassigns its faces.
public interface IFaceRepository
{
    Task<IEnumerable<FaceSyncResult>> SyncPersonStatuses(Guid userId, IEnumerable<PersonStatusSync> statuses, CancellationToken token = default);
    Task<IEnumerable<FaceSyncResult>> SyncPersons(Guid userId, IEnumerable<PersonSync> persons, CancellationToken token = default);
    Task<IEnumerable<FaceSyncResult>> SyncFaces(Guid userId, IEnumerable<FaceSync> faces, CancellationToken token = default);
    Task<IEnumerable<FaceSyncResult>> DeletePersons(Guid userId, IEnumerable<Guid> personIds, CancellationToken token = default);
    Task<IEnumerable<FaceSyncResult>> DeleteFaces(Guid userId, IEnumerable<Guid> faceIds, CancellationToken token = default);

    // guards the published face image endpoints
    Task<bool> FaceExists(Guid userId, Guid faceId, CancellationToken token = default);
}
