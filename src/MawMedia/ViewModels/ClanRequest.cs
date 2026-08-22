namespace MawMedia.ViewModels;

// PersonIds is the whole membership, not a delta: the client is a multi select
// and already knows the full set, which makes the call idempotent and means a
// lost response cannot leave a clan half updated.  null on a rename means "leave
// membership alone".
public record ClanRequest(
    string Name,
    Guid[]? PersonIds
);
