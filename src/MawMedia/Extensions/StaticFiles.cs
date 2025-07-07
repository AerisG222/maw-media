using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace MawMedia.Extensions;

public static class StaticFilesExtensions
{
    public static IApplicationBuilder UseCustomStaticFiles(this IApplicationBuilder app)
    {
        var assetDir = ((WebApplication)app).Configuration.GetValue<string>("AssetsDirectory");

        ArgumentException.ThrowIfNullOrWhiteSpace(assetDir, "Asset directory must be configured");

        if(!Directory.Exists(assetDir))
        {
            throw new DirectoryNotFoundException(assetDir);
        }

        app
            .UseStaticFiles(new StaticFileOptions()
            {
                ContentTypeProvider = new FileExtensionContentTypeProvider(),
                FileProvider = new PhysicalFileProvider(assetDir),
                HttpsCompression = HttpsCompressionMode.DoNotCompress,  // images/videos already optimized, ensure this doesn't trigger
                RequestPath = "/assets"
            });

        return app;
    }
}
