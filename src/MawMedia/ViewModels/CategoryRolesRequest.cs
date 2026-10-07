namespace MawMedia.ViewModels;

// the whole set of roles, not a delta, so the call is idempotent.  an empty list
// is refused rather than read as "visible to nobody".
public record CategoryRolesRequest(
    string[] Roles
);
