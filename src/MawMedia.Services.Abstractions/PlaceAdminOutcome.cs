namespace MawMedia.Services.Abstractions;

// why a merge or a re-parent succeeded or did not.
//
// an enum rather than an exception for the reason PlaceCoverOutcome gives: each of
// these is an ordinary answer an admin screen acts on, and the route maps them to
// different status codes.
public enum PlaceAdminOutcome
{
    Ok,

    // the caller is not an admin.  the shape of the tree is shared by everyone, so
    // reshaping it is not something a reader may do.
    NotAdmin,

    // no such place.  for a merge this covers either side - which one is not
    // reported, since an admin screen holds both ids and can re-read them.
    NotFound,

    // a merge was asked to fold a place into itself
    SamePlace,

    // a merge across kinds - a state into a country.  refused, because folding one
    // level into another is a different mistake rather than a correction.
    KindMismatch,

    // the proposed parent does not sit above the place in the hierarchy, or the
    // move would put a place beneath itself
    InvalidParent,

    // only the shallowest kind may sit at the root of the tree
    NotARootKind
}
