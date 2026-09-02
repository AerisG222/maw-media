namespace MawMedia.ViewModels;

// which of the caller's photographs to publish as a place's cover.
//
// a body rather than a route segment because the operation is a publication, not
// a navigation - PUT /places/{id}/cover names the thing being replaced, and the
// body says what to replace it with.
public record PlaceCoverRequest(
    Guid MediaId
);
