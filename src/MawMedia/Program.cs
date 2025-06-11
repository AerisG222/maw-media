using NodaTime;
using ZiggyCreatures.Caching.Fusion;
using Scalar.AspNetCore;
using MawMedia.Extensions;
using MawMedia.Routes;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration
    .AddEnvironmentVariables("MAW_MEDIA_");

builder.Services
    .AddSystemd()
    .AddCustomCorsPolicy(builder.Configuration)
    .AddCustomDataProtection(builder.Configuration)
    .AddCustomForwardedHeaders(builder.Configuration)
    .AddNpgsql(builder.Configuration)
    .AddFusionCache()
        .AsHybridCache()
        .Services
    .When(builder.Environment.IsDevelopment(), services => {
        services.AddCustomOpenApi(
            "MaW Media API",
            "An API to access photos and videos from media.mikeandwan.us."
        );
    })
    .AddSingleton<IClock>(services => SystemClock.Instance);

var app = builder.Build();

app
    .UseForwardedHeaders()
    .UseHttpsRedirection()
    .UseRouting()
    .UseCustomSecurityHeaders()
    .UseCors()
    // .UseAuthentication()
    // .UseAuthorization()
    .When(app.Environment.IsDevelopment(), app => {
        app.MapOpenApi();
        app.MapScalarApiReference(opts => {
            opts.AddAuthorizationCodeFlow("OAuth2", flow => { });
        });
    });

app.MapGroup("/categories").MapCategoryRoutes();
app.MapGroup("/media").MapMediaRoutes();

await app.RunAsync();
