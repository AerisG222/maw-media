namespace MawMedia.Services.Models;

// media.get_places returns a flat row per place, so unlike CategoryAndTeaser and
// MediaAndFile there is no fan-out to reassemble - this maps one to one onto
// MawMedia.Models.Place.
//
// it exists anyway so the database's shape and the api's shape can move
// independently, which is the point at which the teaser image lands: a column
// added here does not oblige every consumer of Place to change with it.
class PlaceRow
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public required string Kind { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public int MediaCount { get; set; }
    public NodaTime.Instant? CoverCreated { get; set; }
    public Guid? CoverMediaId { get; set; }
    public string[]? AncestorNames { get; set; }
    public int ChildCount { get; set; }
}
