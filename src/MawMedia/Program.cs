using MawMedia.Authorization;
using MawMedia.Extensions;
using MawMedia.Routes;
using MawMedia.Services;
using Microsoft.Net.Http.Headers;
using NodaTime;
using ZiggyCreatures.Caching.Fusion;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost
    .UseKestrelHttpsConfiguration();

builder.Configuration
    .AddEnvironmentVariables("MAW_MEDIA_");

builder.Services
    .AddSystemd()
    .AddCustomCorsPolicy(builder.Configuration)
    .AddCustomDataProtection(builder.Configuration)
    .AddCustomForwardedHeaders(builder.Configuration)
    .ConfigureCustomJsonOptions()
    .AddHeaderPropagation(opts =>
    {
        opts.Headers.Add(HeaderNames.Authorization);
    })
    .AddNpgsql(builder.Configuration)
    .AddFusionCache()
        .AsHybridCache()
        .Services
    .AddCustomOpenApi()
    .AddCustomAuth(builder.Configuration)
    .AddSingleton<IClock>(SystemClock.Instance)
    .AddMediaAuthorizationHandler()
    .AddMediaServices(builder.Configuration);

var app = builder.Build();

app
    .UseForwardedHeaders()
    .UseHeaderPropagation()
    .UseRouting()
    .UseCustomSecurityHeaders()
    .UseCors()
    .UseAuthentication()
    .UseAuthorization()
    .UseCustomStaticFiles()
    .UseCustomOpenApi();

app.MapGroup("/auth").MapAuthRoutes();
app.MapGroup("/categories").MapCategoryRoutes();
app.MapGroup("/config").MapConfigRoutes();
app.MapGroup("/locations").MapLocationRoutes();
app.MapGroup("/media").MapMediaRoutes();
app.MapGroup("/stats").MapStatRoutes();
app.MapGroup("/upload").MapUploadRoutes();

await app.RunAsync();
