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
                        .WithMethods(["GET", "POST", "OPTIONS"])
                        .AllowCredentials()
                        .AllowAnyHeader();
                })
            );

        return services;
    }
}
