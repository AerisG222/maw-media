using NodaTime;

namespace MawMedia.Services.Models;

public record UserStateRecord(
    string ExternalId,
    Instant? ExternalProfileLastUpdated,
    Guid? UserId,
    Instant? UserLastUpdated,
    bool IsAdmin
);
