using MawMedia.Models;

namespace MawMedia.Services.Abstractions;

// browsing media by where it was taken.
//
// deliberately separate from ILocationRepository, which is the geocode
// *maintenance* side: that one finds coordinates missing metadata and writes it
// back, is gated on the location:read / location:write scopes, and hard refuses
// non-admins.  this is ordinary media browsing gated on media:read.  keeping them
// apart is what stops a browse client from needing the correction worker's
// permissions.
//
// every method is scoped by userId, and a place holding nothing the caller may see
// simply does not exist as far as this interface is concerned - the same rule
// IFaceRepository applies to people.
public interface IPlaceRepository
{
    // one level of the drill-down: the countries when parentId is null, otherwise
    // that place's children.  returned whole rather than paged, like GetPersons,
    // because every request is scoped to one parent - the largest answer is one
    // country's states or one state's cities, never "every city".
    //
    // kind is an optional extra filter for a client that wants to group a mixed
    // listing; it is not needed to page or to drill.
    Task<IEnumerable<Place>> GetPlaces(Guid userId, string baseUrl, Guid? parentId = null, string? kind = null, CancellationToken token = default);

    // a single place by id, applying the same access rule - null when the caller
    // may see nothing there.  shares GetPlaces' query rather than adding a second
    // one that could drift from it.
    Task<Place?> GetPlace(Guid userId, string baseUrl, Guid placeId, CancellationToken token = default);

    // the breadcrumb above a place, country first, including the place itself.
    // empty when the id is unknown.
    Task<IEnumerable<PlaceAncestor>> GetPlaceAncestors(Guid userId, Guid placeId, CancellationToken token = default);

    // the drill-in behind a place.  paged, unlike the listing above: one country
    // holds tens of thousands of media.
    //
    // seed=<number> shuffles deterministically so paging stays coherent, exactly
    // as it does for a person's media.
    Task<SearchResult<Media>> GetPlaceMedia(Guid userId, string baseUrl, Guid placeId, int offset, int limit, bool favoritesOnly = false, long? seed = null, CancellationToken token = default);

    // the same media rolled up to the categories holding them, so a place screen
    // can toggle between "the photos" and "the trips".
    //
    // no seed: shuffling exists because a place's media is a browse-forever set,
    // while these are a navigational index and want a stable order.
    //
    // favoritesOnly is broader here than on the media view - it keeps a category
    // the caller favourited outright *or* one holding a media they favourited.
    // both are the caller saying they care about the category, and the screen
    // drives media and categories from a single toggle.
    Task<SearchResult<Category>> GetPlaceCategories(Guid userId, string baseUrl, Guid placeId, int offset, int limit, bool favoritesOnly = false, CancellationToken token = default);

    // --- covers (admin) ---------------------------------------------------
    // choosing a cover publishes a copy of the photograph to a directory served
    // with no authorization check, so these are admin only and the outcome is
    // reported rather than thrown - a caller needs to tell "not yours to do" from
    // "that photo is not at that place".
    Task<PlaceCoverOutcome> SetPlaceCover(Guid userId, Guid placeId, Guid mediaId, CancellationToken token = default);
    Task<PlaceCoverOutcome> ClearPlaceCover(Guid userId, Guid placeId, CancellationToken token = default);

    // --- shape (admin) ----------------------------------------------------
    // the tree is derived from what the geocoder said, and the geocoder is
    // sometimes wrong in ways no normalizer can fix by guessing.  these are how an
    // admin says so explicitly.  both are admin only and both survive
    // re-derivation, because media.place_alias records the correction rather than
    // the place's name.

    // folds one place into another and deletes it: same kind, any parent.  the
    // usual fix for a place the geocoder spelled two ways, or filed twice.
    Task<PlaceAdminOutcome> MergePlaces(Guid userId, Guid winnerId, Guid loserId, CancellationToken token = default);

    // moves a place to a different parent, or to the root when parentId is null.
    // the fix for a place filed in the wrong branch - usually because the geocode
    // came back with no state to hang it from.
    Task<PlaceAdminOutcome> SetPlaceParent(Guid userId, Guid placeId, Guid? parentId, CancellationToken token = default);
}
