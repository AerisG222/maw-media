using Microsoft.AspNetCore.Authorization;

namespace MawMedia.Authorization;

public sealed class ScopeRequirement
    : IAuthorizationRequirement
{
    public string RequiredScope { get; init; }

    public ScopeRequirement(string requiredScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredScope);

        RequiredScope = requiredScope;
    }
}
