using NodaTime;

namespace MawMedia.Services.Models;

public class CategoryAndTeaser
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public LocalDate EffectiveDate { get; set; }
    public Instant Modified { get; set; }
    public bool IsFavorite { get; set; }
    public Guid MediaId { get; set; }
    public required string MediaType { get; set; }
    public required string FilePath { get; set; }
    public required string FileType { get; set; }
    public required string FileScale { get; set; }
}
