namespace MawMedia;

// single source of truth for the API scopes. consumed both by the authorization
// policies (Extensions/Auth.cs) and by the OpenAPI security definitions
// (Extensions/OpenApi.cs) so the two cannot drift apart.
public static class ApiScopes
{
    public const string MediaRead = "media:read";
    public const string MediaWrite = "media:write";
    public const string CommentsRead = "comments:read";
    public const string CommentsWrite = "comments:write";
    public const string LocationRead = "location:read";
    public const string LocationWrite = "location:write";
    public const string StatsRead = "stats:read";

    // spans /config/person-statuses, /persons and /faces, so it is named for the
    // capability rather than any one resource
    public const string FaceRecognitionPublish = "face-recognition:publish";
    public const string FaceRecognitionRead = "face-recognition:read";

    // human readable descriptions surfaced in the Scalar authorization UI
    public static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>
        {
            [MediaRead] = "Read media, metadata and GPS",
            [MediaWrite] = "Modify media, GPS overrides and category teasers",
            [CommentsRead] = "Read comments on media",
            [CommentsWrite] = "Add comments to media",
            [LocationRead] = "Read locations and reverse geocode data",
            [LocationWrite] = "Update location reverse geocode data",
            [StatsRead] = "Read media statistics",
            [FaceRecognitionPublish] = "Publish people and faces from the recognition pipeline",
            [FaceRecognitionRead] = "Read people and faces detected in media"
        };

    // maps an authorization policy name to the scope it requires.
    // AuthorizationPolicies.User is intentionally absent - it only requires
    // an authenticated user and carries no scope requirement.
    public static readonly IReadOnlyDictionary<string, string> ByPolicy =
        new Dictionary<string, string>
        {
            [AuthorizationPolicies.MediaReader] = MediaRead,
            [AuthorizationPolicies.MediaWriter] = MediaWrite,
            [AuthorizationPolicies.CommentsReader] = CommentsRead,
            [AuthorizationPolicies.CommentsWriter] = CommentsWrite,
            [AuthorizationPolicies.LocationReader] = LocationRead,
            [AuthorizationPolicies.LocationWriter] = LocationWrite,
            [AuthorizationPolicies.StatsReader] = StatsRead,
            [AuthorizationPolicies.FaceRecognitionPublisher] = FaceRecognitionPublish,
            [AuthorizationPolicies.FaceRecognitionReader] = FaceRecognitionRead
        };

    // Auth0 issues scopes for a custom API prefixed with the API identifier (audience)
    public static string Qualify(string audience, string scope) =>
        $"{audience}/{scope}";
}
