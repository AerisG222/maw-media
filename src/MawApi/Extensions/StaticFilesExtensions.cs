using Microsoft.AspNetCore.StaticFiles;

namespace MawApi.Extensions;

public static class StaticFilesExtensions
{
    public static IApplicationBuilder UseCustomStaticFiles(this IApplicationBuilder app)
    {
        app
            .UseStaticFiles(new StaticFileOptions {
                ContentTypeProvider = GetCustomMimeTypeProvider()
            });

        return app;
    }

    static FileExtensionContentTypeProvider GetCustomMimeTypeProvider()
    {
        var provider = new FileExtensionContentTypeProvider();

        provider.Mappings[".gltf"] = "model/gltf+json";

        return provider;
    }
}
