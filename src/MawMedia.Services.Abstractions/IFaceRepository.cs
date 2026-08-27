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

    // the clan equivalent: media containing *any* member, with the same paging,
    // favourites filter and seeded shuffle.  a photo holding several members
    // still counts once.
    Task<SearchResult<Media>> GetClanMedia(Guid userId, string baseUrl, Guid clanId, int offset, int limit, bool favoritesOnly = false, long? seed = null, CancellationToken token = default);

    // the same two sets rolled up to the categories holding them, for the faces
    // screen's "categories" toggle.  paged like the media views and returning
    // Category so a client renders them with the category grid it already has.
    //
    // no seed: shuffling exists because a person's media is a browse-forever
    // set, while these are a navigational index and want a stable order.
    //
    // favoritesOnly is broader here than on the media views - it keeps a
    // category the caller favourited outright *or* one holding a media they
    // favourited.  both are the caller saying they care about the category, and
    // the screen drives media and categories from a single toggle.
    Task<SearchResult<Category>> GetPersonCategories(Guid userId, string baseUrl, Guid personId, int offset, int limit, bool favoritesOnly = false, CancellationToken token = default);
    Task<SearchResult<Category>> GetClanCategories(Guid userId, string baseUrl, Guid clanId, int offset, int limit, bool favoritesOnly = false, CancellationToken token = default);

    // --- clans ------------------------------------------------------------
    // a clan is a caller's saved selection of people and is private to them, so
    // every method here is scoped by userId rather than by any shared rule.
    Task<IEnumerable<Clan>> GetClans(Guid userId, string baseUrl, CancellationToken token = default);
    Task<Clan?> GetClan(Guid userId, string baseUrl, Guid clanId, CancellationToken token = default);

    // the outcome codes the clan functions share.  distinguishing them lets the
    // routes answer 404, 400 and 409 rather than collapsing every failure into
    // one status.
    Task<(Guid? ClanId, ClanOutcome Outcome)> CreateClan(Guid userId, string name, Guid[] personIds, CancellationToken token = default);
    Task<ClanOutcome> UpdateClan(Guid userId, Guid clanId, string name, CancellationToken token = default);
    Task<ClanOutcome> SetClanPersons(Guid userId, Guid clanId, Guid[] personIds, CancellationToken token = default);
    Task<ClanOutcome> DeleteClan(Guid userId, Guid clanId, CancellationToken token = default);

    // the faces detected in one media item, with their bounding boxes.  returns
    // an empty collection both for a media item with no faces and for one the
    // caller cannot see - the two need not be told apart, since the media itself
    // already answers that.
    Task<IEnumerable<Face>> GetMediaFaces(Guid userId, Guid mediaId, CancellationToken token = default);

    // guards the face image download, which is served as a static asset under
    // Constants.FaceAssetBaseUrl.  a ValueTask because the result is cached and
    // the picker asks for hundreds of images in a burst.
    ValueTask<bool> CanViewFace(Guid userId, Guid faceId, CancellationToken token = default);
}
