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
                        .AllowAnyHeader();
                })
            );

        return services;
    }
}
