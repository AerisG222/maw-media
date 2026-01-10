using NodaTime;

namespace MawMedia.Models;

public interface IUserState;

public record NonExistentUser : IUserState;

public record NonActivatedUser(
    string ExternalId,
    Instant ExternalProfileLastUpdated
) : IUserState;

public record ActivatedUser(
    string ExternalId,
    Instant ExternalProfileLastUpdated,
    Guid UserId,
    Instant UserLastUpdated,
    bool IsAdmin
) : IUserState;
