using MawMedia.Models;
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

    // guards the face image upload: "has this face been published at all"
    Task<bool> FaceExists(Guid userId, Guid faceId, CancellationToken token = default);

    // --- read side --------------------------------------------------------
    // everything below is filtered by media.user_face, so a caller only ever
    // sees people appearing in media they already have access to.
    Task<IEnumerable<Person>> GetPersons(Guid userId, string baseUrl, CancellationToken token = default);

    // paged, because a single person can appear in thousands of media.  lives
    // here rather than on IMediaRepository despite returning Media: the access
    // rule it enforces is the face one, and keeping it beside GetPersons means
    // there is one place to look when that rule changes.
    Task<SearchResult<Media>> GetPersonMedia(Guid userId, string baseUrl, Guid personId, int offset, int limit, CancellationToken token = default);

    // guards the face image download: "may this caller see this face"
    Task<bool> CanViewFace(Guid userId, Guid faceId, CancellationToken token = default);
}
