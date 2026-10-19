using System.Security.Claims;
using MawMedia.Authorization.Claims;

namespace MawMedia.Services.Tests;

// who the caller is comes from the database, through the identity
// MediaIdentityClaimsTransformation adds - never from the access token.  the
// token's identity is first on the principal, so a claim of the same type inside
// it must not be what these read.
public class MediaIdentityTests
{
    const string CLAIM_USER_ID = "https://media.mikeandwan.us/claimTypes/userId";
    const string CLAIM_USER_STATUS = "https://media.mikeandwan.us/claimTypes/userStatus";
    const string CLAIM_IS_ADMIN = "https://media.mikeandwan.us/claimTypes/isAdmin";

    static ClaimsIdentity Token(Guid userId) =>
        new(
            [
                new Claim("sub", "someone"),
                new Claim(CLAIM_USER_ID, userId.ToString()),
                new Claim(CLAIM_USER_STATUS, "active"),
                new Claim(CLAIM_IS_ADMIN, "True")
            ],
            authenticationType: "Bearer");

    [Fact]
    public void TheDatabaseIdentityWinsOverClaimsInTheToken()
    {
        var real = Guid.CreateVersion7();
        var principal = new ClaimsPrincipal([Token(Guid.CreateVersion7()), MediaIdentity.Create(real, "inactive", false)]);

        Assert.Equal(real, principal.GetMediaUserId());
        Assert.Equal("inactive", principal.GetUserStatus());
        Assert.False(principal.GetIsAdmin());
    }

    [Fact]
    public void ClaimsInTheTokenAloneAreNotAnIdentity()
    {
        // the transformation found no user, or never ran
        var principal = new ClaimsPrincipal(Token(Guid.CreateVersion7()));

        Assert.Null(principal.GetMediaUserId());
        Assert.Equal("inactive", principal.GetUserStatus());
        Assert.False(principal.GetIsAdmin());
    }

    [Fact]
    public void AnInactiveUserHasNoId()
    {
        var principal = new ClaimsPrincipal([Token(Guid.CreateVersion7()), MediaIdentity.Create(null, "inactive", false)]);

        Assert.Null(principal.GetMediaUserId());
    }
}
