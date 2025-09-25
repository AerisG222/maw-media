using NodaTime;

namespace MawMedia.Models;

public interface IUserState;

public record class NonExistentUser() : IUserState;

public record class NonActivatedUser(
    string ExternalId,
    Instant ExternalProfileLastUpdated
) : IUserState;

public record class ActivatedUser(
    string ExternalId,
    Instant ExternalProfileLastUpdated,
    Guid UserId,
    Instant UserLastUpdated,
    bool IsAdmin
) : IUserState;
