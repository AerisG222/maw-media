using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using MawMedia.Models;
using MawMedia.Services.Models;
using NodaTime;
using Npgsql;

namespace MawMedia.Services;

public class AuthRepository
    : BaseRepository, IAuthRepository
{
    readonly HybridCache _cache;
    readonly IUserInfoClient _client;

    public AuthRepository(
        ILogger<AuthRepository> log,
        NpgsqlConnection conn,
        HybridCache cache,
        IUserInfoClient client
    ) : base(log, conn)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(client);

        _cache = cache;
        _client = client;
    }

    public async Task<IUserState> GetUserState(string externalId, CancellationToken token) =>
        await _cache.GetOrCreateAsync(
            CacheKeyBuilder.UserState(externalId),
            async cancel => await InternalGetUserState(externalId),
            cancellationToken: token
        );

    public async Task<bool> GetIsAdmin(Guid userId) =>
        await ExecuteScalar<bool>(
            "SELECT * FROM media.get_is_admin(@userId);",
            new
            {
                userId
            }
        );

    public async Task<IUserState> OnboardExternalIdentity()
    {
        UserInfo? userInfo = null;

        try
        {
            userInfo = await _client.QueryUserInfo();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error obtaining user profile from auth0: {ERROR}", ex.Message);
        }


        if (userInfo == null)
        {
            return new NonExistentUser();
        }

        return await CreateExternalUser(userInfo);
    }

    async Task<IUserState> CreateExternalUser(UserInfo userInfo)
    {
        var userStateRecord = await ExecuteQueryInTransaction<UserStateRecord>(
            """
            SELECT * FROM media.create_external_identity
            (
                @externalId,
                @name,
                @email,
                @emailVerified,
                @givenName,
                @surname,
                @picture
            );
            """,
            new
            {
                externalId = userInfo.Sub,
                name = userInfo.Name,
                email = userInfo.Email,
                emailVerified = userInfo.EmailVerified,
                givenName = userInfo.GivenName,
                surname = userInfo.FamilyName,
                picture = userInfo.Picture
            }
        );

        return BuildUserState(userStateRecord?.SingleOrDefault());
    }

    async Task<IUserState> InternalGetUserState(string externalId)
    {
        var userStateRecord = await QuerySingle<UserStateRecord>(
            "SELECT * FROM media.get_user_state(@external_id);",
            new
            {
                external_id = externalId
            }
        );

        return BuildUserState(userStateRecord);
    }

    IUserState BuildUserState(UserStateRecord? userStateRecord)
    {
        if (userStateRecord == null)
        {
            return new NonExistentUser();
        }

        if (userStateRecord.UserId == null)
        {
            return new NonActivatedUser(userStateRecord.ExternalId, userStateRecord.ExternalProfileLastUpdated ?? Instant.MinValue);
        }

        return new ActivatedUser(
            userStateRecord.ExternalId,
            userStateRecord.ExternalProfileLastUpdated ?? Instant.MinValue,
            userStateRecord.UserId.Value,
            userStateRecord.UserLastUpdated ?? Instant.MinValue,
            userStateRecord.IsAdmin
        );
    }
}
