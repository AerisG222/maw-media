using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MawMedia.Services.Tests.Api;

// stands in for the Auth0 JWT handler.  it deliberately does the bare minimum:
// emit `sub` and `scope` claims from request headers.  everything downstream is
// production code - MediaIdentityClaimsTransformation resolves the sub against
// media.external_identity to attach the media user id and admin flag, and
// ScopeAuthorizationHandler evaluates the scope claim.  that keeps the real
// authorization path under test rather than a stub of it.
public class TestAuthHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SCHEME = "Test";
    public const string HEADER_SUB = "X-Test-Sub";
    public const string HEADER_SCOPES = "X-Test-Scopes";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    ) : base(options, logger, encoder)
    {

    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HEADER_SUB, out var sub) || string.IsNullOrWhiteSpace(sub))
        {
            // no header means an anonymous caller, which must produce a 401
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        List<Claim> claims = [new Claim("sub", sub!)];

        if (Request.Headers.TryGetValue(HEADER_SCOPES, out var scopes) && !string.IsNullOrWhiteSpace(scopes))
        {
            claims.Add(new Claim("scope", scopes!));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SCHEME));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SCHEME)));
    }
}
