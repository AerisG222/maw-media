namespace MawMedia.Models;

// one rung of the breadcrumb above a place: "United States > Massachusetts >
// Boston".
//
// deliberately not Place.  a breadcrumb needs no MediaCount - it labels a path
// rather than offering a tile - and computing one per rung would mean a subtree
// aggregate per level for a number nothing renders.
//
// Depth is 1-based from the country, so a client can render the chain without
// re-deriving the order it was already given in.
public record PlaceAncestor(
    Guid Id,
    Guid? ParentId,
    string Kind,
    string Name,
    string Slug,
    int Depth
);
