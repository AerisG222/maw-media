namespace MawMedia.Models;

// one reason a restriction request was refused.  a refused request returns every
// problem it found rather than the first, so an admin screen can mark each photo
// in a selection at once.
//
// Reason is a stable, lowercase code a client can switch on; see
// media.set_media_roles for the full list.  MediaId is null for problems that
// belong to the request rather than to a photo - an unknown role name, say - and
// Detail carries what the problem is about: the role, the category or the place.
public record MediaRestrictionProblem(
    Guid? MediaId,
    string Reason,
    string? Detail
);
