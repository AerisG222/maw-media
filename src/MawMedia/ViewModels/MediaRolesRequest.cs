namespace MawMedia.ViewModels;

// the whole restriction, not a delta: the client is a multi select and already
// knows the full set, so the call is idempotent.  an empty list is refused rather
// than read as "visible to nobody" - removing a restriction is its own route.
public record MediaRolesRequest(
    string[] Roles
);
