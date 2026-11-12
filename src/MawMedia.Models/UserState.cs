using NodaTime;

namespace MawMedia.Models;

public interface IUserState;

public sealed record NonExistentUser : IUserState;

public sealed record NonActivatedUser(
    string ExternalId,
    Instant ExternalProfileLastUpdated
) : IUserState;

public sealed record ActivatedUser(
    string ExternalId,
    Instant ExternalProfileLastUpdated,
    Guid UserId,
    Instant UserLastUpdated,
    bool IsAdmin
) : IUserState;
