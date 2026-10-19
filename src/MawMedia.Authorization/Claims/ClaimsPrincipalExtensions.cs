using System.Security.Claims;

namespace MawMedia.Authorization.Claims;

// read from the identity MediaIdentityClaimsTransformation added, and only from
// it - see MediaIdentity
public static class ClaimsPrincipalExtensions
{
    public static Guid? GetMediaUserId(this ClaimsPrincipal principal)
    {
        var id = MediaIdentity.FindValue(principal, Constants.CLAIM_USER_ID);

        return id != null
            ? Guid.Parse(id)
            : null;
    }

    public static bool GetIsAdmin(this ClaimsPrincipal principal)
    {
        var isAdmin = MediaIdentity.FindValue(principal, Constants.CLAIM_IS_ADMIN);

        return isAdmin != null && bool.Parse(isAdmin);
    }

    public static string GetUserStatus(this ClaimsPrincipal principal)
    {
        return MediaIdentity.FindValue(principal, Constants.CLAIM_USER_STATUS) ?? Constants.USER_STATUS_INACTIVE;
    }
}
