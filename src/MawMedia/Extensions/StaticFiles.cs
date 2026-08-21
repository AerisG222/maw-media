using MawMedia.Authorization.Claims;
using MawMedia.Services;
using MawMedia.Services.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace MawMedia.Extensions;

public static class StaticFilesExtensions
{
    public static IApplicationBuilder UseCustomStaticFiles(this IApplicationBuilder app)
    {
        var assetDir = RootDirectory(app.ApplicationServices.GetRequiredService<IOptions<AssetConfig>>().Value.RootDirectory);
        var faceDir = RootDirectory(app.ApplicationServices.GetRequiredService<IOptions<FaceImageConfig>>().Value.RootDirectory);

        // face crops first.  the media predicate below matches /assets, which
        // /assets/faces also satisfies, and a UseWhen branch is not terminal - a
        // face request the file provider cannot serve falls through and would
        // then be judged by the media rule.  the media branch excludes the face
        // prefix rather than relying on this ordering, but the ordering is here
        // too so a future edit has to break both to go wrong.
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments(Constants.FaceAssetBaseUrl),
            faces => faces
                .Use(AuthorizeFaceAsset)
                .UseStaticFiles(new StaticFileOptions()
                {
                    ContentTypeProvider = new FileExtensionContentTypeProvider(),
                    FileProvider = new PhysicalFileProvider(faceDir),
                    HttpsCompression = HttpsCompressionMode.DoNotCompress,  // avif is already compressed
                    RequestPath = Constants.FaceAssetBaseUrl
                })
        );

        // assets are served by middleware, so they never match an endpoint and the authorization
        // middleware cannot protect them. branch on the asset prefix and evaluate the policy
        // directly - a request that falls outside this branch is left alone, so an unmatched route
        // reaches the end of the pipeline as a 404 rather than being challenged.
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments(Constants.AssetBaseUrl)
                && !ctx.Request.Path.StartsWithSegments(Constants.FaceAssetBaseUrl),
            assets => assets
                .Use(AuthorizeAsset)
                .UseStaticFiles(new StaticFileOptions()
                {
                    ContentTypeProvider = new FileExtensionContentTypeProvider(),
                    FileProvider = new PhysicalFileProvider(assetDir),
                    HttpsCompression = HttpsCompressionMode.DoNotCompress,  // images/videos already optimized, ensure this doesn't trigger
                    RequestPath = Constants.AssetBaseUrl
                })
        );

        return app;
    }

    static string RootDirectory(string? configured)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configured);

        if (!Directory.Exists(configured))
        {
            throw new DirectoryNotFoundException(configured);
        }

        return configured;
    }

    static Task AuthorizeAsset(HttpContext ctx, RequestDelegate next) =>
        Authorize(ctx, next, AuthorizationPolicies.MediaStaticAsset);

    // two steps rather than one policy, because the two failures have to answer
    // differently.  lacking the scope is a statement about the client and is a
    // plain 403.  being unable to see a particular face must be a 404 - a 403
    // would confirm the face exists, which is the leak the api route this
    // replaced was careful to avoid.
    static async Task AuthorizeFaceAsset(HttpContext ctx, RequestDelegate next)
    {
        var authorization = ctx.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(ctx.User, ctx, AuthorizationPolicies.FaceStaticAsset);

        if (!result.Succeeded)
        {
            await ChallengeOrForbid(ctx);

            return;
        }

        var userId = ctx.User.GetMediaUserId();

        if (userId == null || !TryGetFaceId(ctx.Request.Path, out var faceId))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;

            return;
        }

        var repo = ctx.RequestServices.GetRequiredService<IFaceRepository>();

        if (!await repo.CanViewFace(userId.Value, faceId, ctx.RequestAborted))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;

            return;
        }

        await next(ctx);
    }

    // the path is the only thing naming the face, and it is caller input rather
    // than a bound route value.  anything that is not exactly "{guid}.avif"
    // directly under the prefix is refused here rather than reaching the file
    // provider - a nested path, a traversal segment or another extension all
    // fail this.
    static bool TryGetFaceId(PathString path, out Guid faceId)
    {
        faceId = Guid.Empty;

        var value = path.Value;

        if (value == null || !value.StartsWith(Constants.FaceAssetBaseUrlWithSlash, StringComparison.Ordinal))
        {
            return false;
        }

        var name = value[Constants.FaceAssetBaseUrlWithSlash.Length..];

        if (name.Contains('/', StringComparison.Ordinal) ||
            !name.EndsWith(Constants.FaceImageExtension, StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParse(name[..^Constants.FaceImageExtension.Length], out faceId);
    }

    static async Task Authorize(HttpContext ctx, RequestDelegate next, string policy)
    {
        var authorization = ctx.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(ctx.User, ctx, policy);

        if (result.Succeeded)
        {
            await next(ctx);

            return;
        }

        await ChallengeOrForbid(ctx);
    }

    // mirrors how the authorization middleware answers an endpoint: challenge a caller who
    // has not authenticated (401 plus WWW-Authenticate), forbid one who has (403)
    static async Task ChallengeOrForbid(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            await ctx.ForbidAsync();
        }
        else
        {
            await ctx.ChallengeAsync();
        }
    }
}
