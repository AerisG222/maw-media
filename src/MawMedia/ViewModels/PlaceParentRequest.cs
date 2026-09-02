namespace MawMedia.ViewModels;

// where to move a place to.
//
// ParentId is nullable, and null is meaningful rather than missing: it moves the
// place to the root of the tree.  only a country may go there.
public record PlaceParentRequest(
    Guid? ParentId
);
