using MawMedia.Services;
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
                ContentTypeProvider = new MyExt(),
                FileProvider = new PhysicalFileProvider(assetDir),
                HttpsCompression = HttpsCompressionMode.DoNotCompress,  // images/videos already optimized, ensure this doesn't trigger
                RequestPath = Constants.AssetBaseUrl
            });

        return app;
    }
}

// todo: this should be included by default in net10, so remove this then
public class MyExt : FileExtensionContentTypeProvider
{
    public MyExt()
    {
        Mappings.Add(".avif", "image/avif");
    }
}
