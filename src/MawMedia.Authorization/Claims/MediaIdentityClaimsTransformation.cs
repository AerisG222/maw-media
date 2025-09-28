using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using MawMedia.Models;
using MawMedia.Services;

namespace MawMedia.Authorization.Claims;

public class MediaIdentityClaimsTransformation
    : IClaimsTransformation
{
    readonly IAuthRepository _repo;

    public MediaIdentityClaimsTransformation(IAuthRepository authRepo)
    {
        ArgumentNullException.ThrowIfNull(authRepo);

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
            AddMediaIdentityClaims(principal, null, Constants.USER_STATUS_INACTIVE, false);
        }
        else if (userState is NonExistentUser)
        {
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
