using NodaTime;

namespace MawMedia.Services.Models;

public sealed record UserStateRecord(
    string ExternalId,
    Instant? ExternalProfileLastUpdated,
    Guid? UserId,
    Instant? UserLastUpdated,
    bool IsAdmin
);
