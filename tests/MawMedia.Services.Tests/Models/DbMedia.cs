using System.Text.Json;
using NodaTime;

namespace MawMedia.Services.Tests.Models;

public record DbMedia(
    Guid Id,
    Guid TypeId,
    Guid? LocationId,
    Guid? LocationOverrideId,
    Instant Created,
    Guid CreatedBy,
    Instant Modified,
    Guid ModifiedBy,
    JsonDocument Metadata
);
