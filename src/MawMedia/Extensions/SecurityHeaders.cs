namespace MawMedia.Extensions;

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseCustomSecurityHeaders(this IApplicationBuilder app)
    {
        app
            .UseSecurityHeaders(policies => {
                policies
                    .AddDefaultSecurityHeaders()
                    .AddCrossOriginResourcePolicy(opts =>
                    {
                        // this enables service worker to be able to load images
                        opts.CrossOrigin();
                    });
            });

        return app;
    }
}
