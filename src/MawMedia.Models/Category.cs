using NodaTime;

namespace MawMedia.Models;

public record Category(
    Guid Id,
    string Name,
    LocalDate EffectiveDate,
    Instant Modified,
    bool IsFavorite,
    Media Teaser
);
