using Microsoft.AspNetCore.Authorization;

namespace MawMedia.Authorization;

public static class AuthorizationPolicyBuilderExtensions
{
    public static AuthorizationPolicyBuilder RequireScope(
            this AuthorizationPolicyBuilder authorizationPolicyBuilder,
            string requiredScope)
    {
        authorizationPolicyBuilder
            .Requirements.Add(new ScopeRequirement(requiredScope));

        return authorizationPolicyBuilder;
    }
}
