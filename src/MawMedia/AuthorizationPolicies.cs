namespace MawMedia;

public static class AuthorizationPolicies
{
    public const string User = "user";

    public const string MediaReader = "media:read";
    public const string MediaWriter = "media:write";
    public const string CommentsReader = "comments:read";
    public const string CommentsWriter = "comments:write";
    public const string LocationReader = "location:read";
    public const string LocationWriter = "location:write";
    public const string StatsReader = "stats:read";

    // guards the static asset branch. unlike the policies above it is never attached to an
    // endpoint - assets are served by middleware, so StaticFilesExtensions evaluates it
    // directly. it carries no scope, so it is intentionally absent from ApiScopes.ByPolicy.
    public const string MediaStaticAsset = "media:static-asset";
}
