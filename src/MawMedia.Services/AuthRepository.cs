using Microsoft.Extensions.Logging;
using MawMedia.Models;
using MawMedia.Services.Models;
using NodaTime;
using Npgsql;

namespace MawMedia.Services;

public class AuthRepository
    : BaseRepository, IAuthRepository
{
    public AuthRepository(
        ILogger<AuthRepository> log,
        NpgsqlConnection conn
    ) : base(log, conn)
    {

    }

    public async Task<IUserState> GetUserState(string externalId)
    {
        var state = await QuerySingle<UserStateRecord>(
            "SELECT * FROM media.get_user_state(@external_id);",
            new
            {
                external_id = externalId
            }
        );

        if (state == null)
        {
            return new NonExistentUser();
        }

        if (state.UserId == null)
        {
            return new NonActivatedUser(state.ExternalId, state.ExternalProfileLastUpdated ?? Instant.MinValue);
        }

        return new ActivatedUser(
            state.ExternalId,
            state.ExternalProfileLastUpdated ?? Instant.MinValue,
            state.UserId.Value,
            state.UserLastUpdated ?? Instant.MinValue,
            state.IsAdmin
        );
    }

    public async Task<bool> GetIsAdmin(Guid userId) =>
        await ExecuteScalar<bool>(
            "SELECT * FROM media.get_is_admin(@userId);",
            new
            {
                userId
            }
        );
}
