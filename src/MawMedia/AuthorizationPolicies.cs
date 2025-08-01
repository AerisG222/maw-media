namespace MawMedia;

public static class AuthorizationPolicies
{
    public const string User = "user";
    public const string Admin = "admin";

    public const string MediaReader = "media:read";
    public const string MediaWriter = "media:write";
    public const string CommentReader = "comment:read";
    public const string CommentWriter = "comment:write";
    public const string StatsReader = "stats:read";
}
