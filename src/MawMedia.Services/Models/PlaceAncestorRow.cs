namespace MawMedia.Services.Models;

// media.get_place_ancestors prefixes its output columns with ancestor_ so that
// postgres does not substitute them into the column references in its own body -
// see the comment there.  that prefix is a database concern, so it is unwound here
// rather than leaking into MawMedia.Models.PlaceAncestor.
class PlaceAncestorRow
{
    public Guid AncestorId { get; set; }
    public Guid? AncestorParentId { get; set; }
    public required string AncestorKind { get; set; }
    public required string AncestorName { get; set; }
    public required string AncestorSlug { get; set; }
    public int AncestorDepth { get; set; }
}
