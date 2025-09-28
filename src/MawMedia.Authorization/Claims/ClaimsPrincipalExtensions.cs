using System.Security.Claims;

namespace MawMedia.Authorization.Claims;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetMediaUserId(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(Constants.CLAIM_USER_ID);

        return id != null
            ? Guid.Parse(id)
            : null;
    }

    public static bool GetIsAdmin(this ClaimsPrincipal principal)
    {
        var isAdmin = principal.FindFirstValue(Constants.CLAIM_IS_ADMIN);

        return isAdmin != null && bool.Parse(isAdmin);
    }

    public static string GetUserStatus(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(Constants.CLAIM_USER_STATUS) ?? Constants.USER_STATUS_INACTIVE;
    }
}
