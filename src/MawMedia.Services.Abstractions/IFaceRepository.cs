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
    Task<IEnumerable<Person>> GetPersons(Guid userId, string baseUrl, bool favoritesOnly = false, CancellationToken token = default);

    // the same shape and the same access rule as GetPersons, narrowed to one
    // person, so a caller holding an id does not need a second endpoint whose
    // visibility rule could drift from the list's
    Task<Person?> GetPerson(Guid userId, string baseUrl, Guid personId, CancellationToken token = default);

    // favouriting is per user and only permitted for a person the caller can
    // already see.  false means the person is not visible or does not exist -
    // the caller is told those apart no more than GetPersons tells them.
    Task<bool> SetPersonIsFavorite(Guid userId, Guid personId, bool isFavorite, CancellationToken token = default);

    // paged, because a single person can appear in thousands of media.  lives
    // here rather than on IMediaRepository despite returning Media: the access
    // rule it enforces is the face one, and keeping it beside GetPersons means
    // there is one place to look when that rule changes.
    //
    // seed selects a shuffled order instead of newest first.  it is a seed rather
    // than a bool because the result is paged: a fresh RANDOM() per request would
    // make page 2 repeat and skip rows from page 1, while the same seed always
    // produces the same order.
    Task<SearchResult<Media>> GetPersonMedia(Guid userId, string baseUrl, Guid personId, int offset, int limit, bool favoritesOnly = false, long? seed = null, CancellationToken token = default);

    // guards the face image download, which is served as a static asset under
    // Constants.FaceAssetBaseUrl.  a ValueTask because the result is cached and
    // the picker asks for hundreds of images in a burst.
    ValueTask<bool> CanViewFace(Guid userId, Guid faceId, CancellationToken token = default);
}
