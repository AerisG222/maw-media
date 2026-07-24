namespace MawMedia.Extensions;

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseCustomSecurityHeaders(
        this IApplicationBuilder app,
        IWebHostEnvironment env
    )
    {
        app
            .UseSecurityHeaders(policies =>
            {
                policies
                    .AddDefaultSecurityHeaders()
                    .AddCrossOriginResourcePolicy(opts =>
                    {
                        // this enables service worker to be able to load images
                        opts.CrossOrigin();
                    });

                if (env.IsDevelopment())
                {
                    // Scalar runs the OAuth2 login in a popup that visits Auth0 and then
                    // redirects back to this origin, where it posts the code to the opener.
                    // the default COOP/COEP break that handshake and must be fully relaxed
                    // for the Development-only docs UI:
                    //   - COOP must be `unsafe-none` (not same-origin-allow-popups): the
                    //     popup returns here from Auth0 (unsafe-none). any non-matching COOP
                    //     on this return navigation forces a browsing-context-group switch
                    //     that nulls window.opener, so the code can never be delivered.
                    //   - COEP `credentialless` likewise severs the opener across the
                    //     cross-origin (Auth0) hop.
                    policies
                        .AddCrossOriginOpenerPolicy(opts => opts.UnsafeNone())
                        .AddCrossOriginEmbedderPolicy(opts => opts.UnsafeNone());
                }
            });

        return app;
    }
}
