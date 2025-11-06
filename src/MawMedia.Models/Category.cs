using NodaTime;

namespace MawMedia.Models;

public record Category(
    Guid Id,
    short Year,
    string Slug,
    string Name,
    LocalDate EffectiveDate,
    Instant Modified,
    bool IsFavorite,
    Media Teaser
);
