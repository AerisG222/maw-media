using Microsoft.AspNetCore.Authorization;

namespace MawMedia.Authorization;

public class ScopeAuthorizationHandler
    : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement
    )
    {
        var scopeClaims = context.User.FindAll("scp")
            .Union(context.User.FindAll("scope"))
            .ToList();

        if (scopeClaims.Count == 0)
        {
            return Task.CompletedTask;
        }

        var hasScope = scopeClaims
            .SelectMany(s => s.Value.Split(' '))
            .Contains(requirement.RequiredScope, StringComparer.Ordinal);

        if (hasScope)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
