using MawMedia.Models;

namespace MawMedia.Services.Abstractions;

// why a change to a media's restriction succeeded or did not.  an enum for the
// reason PlaceCoverOutcome gives: each is an ordinary answer a client acts on, and
// each maps to a different status.
public enum MediaRestrictionOutcome
{
    Ok,

    // restrictions decide who may see what across the whole library, so only an
    // admin may change them
    NotAdmin,

    // no such media
    NotFound,

    // the request was refused for the reasons in its problems - an unknown role, a
    // role the category never grants, or a photo that is a teaser or place cover.
    // nothing was changed.
    Invalid
}

public record MediaRestrictionResult(
    MediaRestrictionOutcome Outcome,
    IReadOnlyList<MediaRestrictionProblem> Problems
);

// the roles a media is restricted to.  Roles is empty when it has no restriction
// and follows its category; it is only meaningful when Outcome is Ok.
public record MediaRolesResult(
    MediaRestrictionOutcome Outcome,
    IReadOnlyList<string> Roles
);
