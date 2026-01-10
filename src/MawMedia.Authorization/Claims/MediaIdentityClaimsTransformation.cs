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
            _log.LogInformation("User with external id {EXTERNAL_ID} is not activated yet!", subClaim);

            AddMediaIdentityClaims(principal, null, Constants.USER_STATUS_INACTIVE, false);
        }
        else if (userState is NonExistentUser)
        {
            _log.LogInformation("User with external id {EXTERNAL_ID} has not been onboarded yet!", subClaim);

            userState = await _repo.OnboardExternalIdentity();

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

    static void AddMediaIdentityClaims(ClaimsPrincipal principal, Guid? userId, string status, bool isAdmin)
    {
        var claimsIdentity = new ClaimsIdentity();

        if (userId != null)
        {
            claimsIdentity.AddClaim(new Claim(Constants.CLAIM_USER_ID, userId.Value.ToString()));
        }

        claimsIdentity.AddClaim(new Claim(Constants.CLAIM_USER_STATUS, status));
        claimsIdentity.AddClaim(new Claim(Constants.CLAIM_IS_ADMIN, isAdmin.ToString()));

        principal.AddIdentity(claimsIdentity);
    }
}
