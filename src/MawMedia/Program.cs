using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using ZiggyCreatures.Caching.Fusion;
using Scalar.AspNetCore;
using MawMedia.Extensions;
using MawMedia.Routes;
using MawMedia.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration
    .AddEnvironmentVariables("MAW_MEDIA_");

builder.Services
    .AddSystemd()
    .AddCustomCorsPolicy(builder.Configuration)
    .AddCustomDataProtection(builder.Configuration)
    .AddCustomForwardedHeaders(builder.Configuration)
    .AddNpgsql(builder.Configuration)
    .ConfigureHttpJsonOptions(opts =>
    {
        opts.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
    })
    .AddFusionCache()
        .AsHybridCache()
        .Services
    .When(builder.Environment.IsDevelopment(), services =>
    {
        services.AddCustomOpenApi(
            "MaW Media API",
            "An API to access photos and videos from media.mikeandwan.us."
        );
    })
    .AddSingleton<IClock>(services => SystemClock.Instance)
    .AddMediaServices();

var app = builder.Build();

app
    .UseForwardedHeaders()
    .UseHttpsRedirection()
    .UseRouting()
    .UseCustomSecurityHeaders()
    .UseCors()
    // .UseAuthentication()
    // .UseAuthorization()
    .UseCustomStaticFiles()
    .When(app.Environment.IsDevelopment(), app => {
        app.MapOpenApi();
        app.MapScalarApiReference(opts => {
            opts.AddAuthorizationCodeFlow("OAuth2", flow => { });
        });
    });

app.MapGroup("/categories").MapCategoryRoutes();
app.MapGroup("/media").MapMediaRoutes();
app.MapGroup("/stats").MapStatRoutes();

await app.RunAsync();
