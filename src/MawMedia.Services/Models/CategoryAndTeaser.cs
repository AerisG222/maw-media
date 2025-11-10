using NodaTime;

namespace MawMedia.Services.Models;

class CategoryAndTeaser
{
    public Guid Id { get; set; }
    public short Year { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public LocalDate EffectiveDate { get; set; }
    public Instant Modified { get; set; }
    public bool IsFavorite { get; set; }
    public Guid MediaId { get; set; }
    public required string MediaSlug { get; set; }
    public required string MediaType { get; set; }
    public bool MediaIsFavorite { get; set; }
    public required Guid FileId { get; set; }
    public required string FilePath { get; set; }
    public required string FileType { get; set; }
    public required string FileScale { get; set; }
}
