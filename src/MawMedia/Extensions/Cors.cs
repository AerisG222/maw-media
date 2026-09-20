namespace MawMedia.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddCustomCorsPolicy(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        var allowedOrigins = config.GetSection("CorsOriginUrls").Get<string[]>();

        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            Console.WriteLine("No CORS origin configured in settings - skipping CORS configuration!");

            return services;
        }

        services
            .AddCors(opts =>
                opts.AddDefaultPolicy(builder =>
                {
                    builder
                        .WithOrigins([.. allowedOrigins])
                        // this list has to track the verbs the routes actually
                        // expose.  DELETE was missed when clans introduced the
                        // api's first one, and the symptom is browser only - the
                        // preflight is rejected, so the call never reaches the
                        // endpoint and every server side test still passes.
                        .WithMethods(["GET", "POST", "PUT", "DELETE", "OPTIONS"])
                        .AllowCredentials()
                        .AllowAnyHeader()
                        // every call this api serves carries an Authorization
                        // header, which is never a "simple" request, so the
                        // browser preflights all of them.  without a max age it
                        // preflights each one *again* on every call - an extra
                        // round trip to a cross origin host before any request
                        // that matters can start.
                        //
                        // an hour rather than longer because chrome caps the value
                        // at two hours whatever is sent, and the cost of the cache
                        // is a stale policy: change the verbs above and a browser
                        // may hold the old answer for up to this long.  the
                        // comment above is the reason that is worth bounding - a
                        // missed verb is already a browser only failure, and it
                        // would be worse to have it outlive the fix by a day.
                        .SetPreflightMaxAge(TimeSpan.FromHours(1));
                })
            );

        return services;
    }
}
