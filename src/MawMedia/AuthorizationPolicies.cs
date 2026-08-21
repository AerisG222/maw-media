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
    public const string FaceRecognitionPublisher = "face-recognition:publish";
    public const string FaceRecognitionReader = "face-recognition:read";

    // guards the static asset branch. unlike the policies above it is never attached to an
    // endpoint - assets are served by middleware, so StaticFilesExtensions evaluates it
    // directly. it carries no scope, so it is intentionally absent from ApiScopes.ByPolicy.
    public const string MediaStaticAsset = "media:static-asset";

    // guards the face crop branch of the static asset pipeline.  unlike
    // MediaStaticAsset it does carry a scope - the whole face read side is gated
    // behind face-recognition:read so it can be revoked per client without
    // touching media access - but it is still evaluated by hand rather than
    // attached to an endpoint, so it stays out of ApiScopes.ByPolicy, which only
    // describes endpoints for OpenAPI.  it answers "may this client use the face
    // feature at all", never "may it see this face"; see StaticFilesExtensions.
    public const string FaceStaticAsset = "face:static-asset";
}
