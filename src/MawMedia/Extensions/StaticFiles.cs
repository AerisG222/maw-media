using MawMedia.Services;
using MawMedia.Services.Abstractions;
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

        app
            .UseStaticFiles(new StaticFileOptions()
            {
                ContentTypeProvider = new FileExtensionContentTypeProvider(),
                FileProvider = new PhysicalFileProvider(assetDir),
                HttpsCompression = HttpsCompressionMode.DoNotCompress,  // images/videos already optimized, ensure this doesn't trigger
                RequestPath = Constants.AssetBaseUrl
            });

        return app;
    }
}
