using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using MawMedia.Models;
using MawMedia.Services.Abstractions;

namespace MawMedia.Authorization.Claims;

public class MediaIdentityClaimsTransformation
    : IClaimsTransformation
{
    readonly ILogger _log;
    readonly IAuthRepository _repo;

    public MediaIdentityClaimsTransformation(
        ILogger<MediaIdentityClaimsTransformation> log,
        IAuthRepository authRepo
    )
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(authRepo);

        _log = log;
        _repo = authRepo;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var subClaim = principal.FindFirstValue("sub");

        if (subClaim == null)
        {
            return principal;
        }

        var userState = await _repo.GetUserState(subClaim, default);

        if (userState is ActivatedUser activatedUser)
        {
            AddMediaIdentityClaims(principal, activatedUser.UserId, Constants.USER_STATUS_ACTIVE, activatedUser.IsAdmin);
        }
        else if (userState is NonActivatedUser)
        {
            ClaimsTransformationLog.NotActivated(_log, subClaim);

            AddMediaIdentityClaims(principal, null, Constants.USER_STATUS_INACTIVE, false);
        }
        else if (userState is NonExistentUser)
        {
            ClaimsTransformationLog.NotOnboarded(_log, subClaim);

            userState = await _repo.OnboardExternalIdentity(default);

            if (userState is ActivatedUser newlyActivatedUser)
            {
                AddMediaIdentityClaims(principal, newlyActivatedUser.UserId, Constants.USER_STATUS_ACTIVE, newlyActivatedUser.IsAdmin);
            }
            else if (userState is NonActivatedUser)
            {
                AddMediaIdentityClaims(principal, null, Constants.USER_STATUS_INACTIVE, false);
            }
        }

        return principal;
    }

    static void AddMediaIdentityClaims(ClaimsPrincipal principal, Guid? userId, string status, bool isAdmin) =>
        principal.AddIdentity(MediaIdentity.Create(userId, status, isAdmin));
}

// source generated: claims transformation runs on every authenticated request, so a
// user waiting on activation logs once per api call and per image
static partial class ClaimsTransformationLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "User with external id {EXTERNAL_ID} is not activated yet!")]
    public static partial void NotActivated(ILogger logger, string external_id);

    [LoggerMessage(Level = LogLevel.Information, Message = "User with external id {EXTERNAL_ID} has not been onboarded yet!")]
    public static partial void NotOnboarded(ILogger logger, string external_id);
}
