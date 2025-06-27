using NodaTime;

namespace MawMedia.Services.Tests.Models;

public record DbCategory(
    Guid Id,
    string Name,
    LocalDate EffectiveDate,
    Instant Created,
    Guid CreatedBy,
    Instant Modified,
    Guid ModifiedBy
);
