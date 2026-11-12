using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace MawMedia.Authorization;

public sealed class ScopeAuthorizationHandler
    : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement
    )
    {
        // runs on every api call and every asset request, so it walks the claims
        // and splits the scope lists in place rather than building collections
        foreach (var claim in context.User.Claims)
        {
            if (IsScopeClaim(claim) && ContainsScope(claim.Value, requirement.RequiredScope))
            {
                context.Succeed(requirement);
                break;
            }
        }

        return Task.CompletedTask;
    }

    static bool IsScopeClaim(Claim claim) =>
        string.Equals(claim.Type, "scp", StringComparison.Ordinal) ||
        string.Equals(claim.Type, "scope", StringComparison.Ordinal);

    static bool ContainsScope(string scopes, string requiredScope)
    {
        var span = scopes.AsSpan();

        foreach (var range in span.Split(' '))
        {
            if (span[range].Equals(requiredScope, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
