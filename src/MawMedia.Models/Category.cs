using NodaTime;

namespace MawMedia.Models;

public record Category(
    Guid Id,
    string Name,
    LocalDate EffectiveDate,
    string QqvgFillPath,
    string? QvgFillPath,
    bool IsFavorite
);
