namespace MawMedia.Extensions;

// before versioning, every route group was mapped at the site root (/media, /categories, ...) and
// a handful of the mutating endpoints answered POST. versioning moved them under /api/v{version}
// and settled those endpoints on PUT, which breaks any client still calling the old paths. this
// restores the old surface before routing runs by rewriting the path - and, where it changed, the
// verb - so a legacy request is served by the v1 endpoint itself: same authorization, same CORS
// policy, same response, rather than a redirect that browsers and preflights handle inconsistently.
//
// this is a migration shim: once nothing calls the root paths, delete this file and its
// UseUnversionedApiCompatibility() call in Program.cs.
public static class UnversionedApiCompatibilityExtensions
{
    // pinned to v1 deliberately: clients on the unversioned paths were written against the v1
    // contract, so they must keep landing on v1 even after the default version moves forward.
    static readonly string VersionedPrefix = $"/api/{ApiVersioningExtensions.DocumentName(ApiVersioningExtensions.V1)}";

    // the route groups that were reachable at the root - keep in sync with the groups mapped
    // under `api` in Program.cs. the rewrite has to run before routing, so these cannot be
    // discovered from the endpoints themselves.
    static readonly string[] LegacySegments =
    [
        "/auth",
        "/categories",
        "/config",
        "/locations",
        "/media",
        "/stats",
        "/upload"
    ];

    // endpoints that answered POST before versioning and answer PUT now. the paths did not
    // change, so each is identified by the group and trailing action of the unversioned
    // /{group}/{id}/{action} shape.
    static readonly (string Group, string Action)[] PostBecamePut =
    [
        ("categories", "favorite"),
        ("categories", "teaser"),
        ("locations", "metadata"),
        ("media", "favorite"),
        ("media", "gps")
    ];

    public static IApplicationBuilder UseUnversionedApiCompatibility(this IApplicationBuilder app)
    {
        var log = app.ApplicationServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(UnversionedApiCompatibilityExtensions).FullName!);

        return app.Use(async (ctx, next) =>
        {
            var legacyPath = ctx.Request.Path;

            if (!TryGetVersionedPath(legacyPath, out var versionedPath))
            {
                await next(ctx);

                return;
            }

            // the verb rewrite is scoped to the unversioned paths on purpose: v1 accepts only PUT
            // for these endpoints, and a caller already using /api/v1 followed the current docs.
            var replayAsPut = HttpMethods.IsPost(ctx.Request.Method) && BecamePut(legacyPath);

            // Debug rather than Information: legacy traffic can be high volume. turn this
            // category up when deciding whether the shim can be removed.
            log.LogDebug(
                "rewriting unversioned request {LegacyMethod} {LegacyPath} to {Method} {VersionedPath}",
                ctx.Request.Method,
                legacyPath,
                replayAsPut ? HttpMethods.Put : ctx.Request.Method,
                versionedPath
            );

            // point the caller at where the endpoint now lives (RFC 5829). ReportApiVersions
            // adds api-supported-versions on the way out.
            ctx.Response.Headers.Link = $"<{versionedPath}{ctx.Request.QueryString}>; rel=\"successor-version\"";

            ctx.Request.Path = versionedPath;

            if (replayAsPut)
            {
                ctx.Request.Method = HttpMethods.Put;
            }

            await next(ctx);
        });
    }

    static bool TryGetVersionedPath(PathString path, out PathString versionedPath)
    {
        foreach (var segment in LegacySegments)
        {
            // matches the group root (/categories) as well as anything below it (/categories/{id}),
            // but not a path that merely starts with the same characters (/categoriesx)
            if (path.StartsWithSegments(segment, StringComparison.OrdinalIgnoreCase))
            {
                versionedPath = new PathString(VersionedPrefix + path.Value);

                return true;
            }
        }

        versionedPath = default;

        return false;
    }

    // true when the unversioned path names one of the endpoints whose verb changed. the id
    // segment is not inspected - routing rejects a malformed one on the versioned route, the
    // same as it would have on the unversioned one.
    static bool BecamePut(PathString path)
    {
        var segments = (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 3)
        {
            return false;
        }

        foreach (var (group, action) in PostBecamePut)
        {
            if (segments[0].Equals(group, StringComparison.OrdinalIgnoreCase)
                && segments[2].Equals(action, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
