using System.Security.Claims;

namespace MawMedia.Authorization.Claims;

// the identity MediaIdentityClaimsTransformation adds to a caller: who they are in
// this system, whether they are active, and whether they are an admin.
//
// these claims are read only from this identity, never from the principal as a
// whole.  the principal also carries the identity built from the access token, and
// it comes first - so reading "the first userId claim anywhere" would let a token
// that happened to carry a claim of the same type decide who the caller is, ahead
// of the database.  the label is what tells the two apart.
public static class MediaIdentity
{
    const string LABEL = "maw-media";

    public static ClaimsIdentity Create(Guid? userId, string status, bool isAdmin)
    {
        var identity = new ClaimsIdentity { Label = LABEL };

        if (userId != null)
        {
            identity.AddClaim(new Claim(Constants.CLAIM_USER_ID, userId.Value.ToString()));
        }

        identity.AddClaim(new Claim(Constants.CLAIM_USER_STATUS, status));
        identity.AddClaim(new Claim(Constants.CLAIM_IS_ADMIN, isAdmin.ToString()));

        return identity;
    }

    internal static string? FindValue(ClaimsPrincipal principal, string claimType) =>
        principal.Identities
            .FirstOrDefault(i => string.Equals(i.Label, LABEL, StringComparison.Ordinal))
            ?.FindFirst(claimType)
            ?.Value;
}
