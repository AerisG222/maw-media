using MawMedia.Models;

namespace MawMedia.Services.Abstractions;

// why a change to a category's roles succeeded or did not; see
// media.set_category_roles.  the same shape as MediaRestrictionOutcome, and for
// the same reason: each is an ordinary answer, mapped to its own status.
public enum CategoryRolesOutcome
{
    Ok,

    NotAdmin,

    NotFound,

    // refused for the reasons in its problems; nothing was changed
    Invalid
}

public record CategoryRolesResult(
    CategoryRolesOutcome Outcome,
    IReadOnlyList<CategoryRolesProblem> Problems
);

// the roles a category is granted to; only meaningful when Outcome is Ok
public record CategoryRolesLookup(
    CategoryRolesOutcome Outcome,
    IReadOnlyList<string> Roles
);
