namespace MawMedia.ViewModels;

// which place to fold into the one named in the route.
//
// the route names the survivor and the body names what disappears, so the url
// still identifies a place that exists after the call - which is what lets the
// response be the merged place rather than a bare status.
public record PlaceMergeRequest(
    Guid SourceId
);
