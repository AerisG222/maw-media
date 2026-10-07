namespace MawMedia.Models;

// one reason a change to a category's roles was refused.  every problem found is
// returned, not just the first.
//
// Reason is a stable, lowercase code; see media.set_category_roles for the list.
// MediaId is set only for restriction_depends - the restricted media whose
// restriction names a role being removed - and Detail carries the role.
public record CategoryRolesProblem(
    Guid? MediaId,
    string Reason,
    string? Detail
);
