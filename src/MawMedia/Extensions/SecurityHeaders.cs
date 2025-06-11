namespace MawMedia.Extensions;

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseCustomSecurityHeaders(this IApplicationBuilder app)
    {
        app
            .UseSecurityHeaders(policies => {
                policies
                    .AddDefaultSecurityHeaders();
            });

        return app;
    }
}
