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
        var assetConfig = app.ApplicationServices.GetRequiredService<IOptions<AssetConfig>>();

        ArgumentNullException.ThrowIfNull(assetConfig);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetConfig.Value.RootDirectory);

        var assetDir = assetConfig.Value.RootDirectory;

        if (!Directory.Exists(assetDir))
        {
            throw new DirectoryNotFoundException(assetDir);
        }

        // assets are served by middleware, so they never match an endpoint and the authorization
        // middleware cannot protect them. branch on the asset prefix and evaluate the policy
        // directly - a request that falls outside this branch is left alone, so an unmatched route
        // reaches the end of the pipeline as a 404 rather than being challenged.
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments(Constants.AssetBaseUrl),
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

    static async Task AuthorizeAsset(HttpContext ctx, RequestDelegate next)
    {
        var authorization = ctx.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(ctx.User, ctx, AuthorizationPolicies.MediaStaticAsset);

        if (result.Succeeded)
        {
            await next(ctx);

            return;
        }

        // mirrors how the authorization middleware answers an endpoint: challenge a caller who
        // has not authenticated (401 plus WWW-Authenticate), forbid one who has (403)
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
