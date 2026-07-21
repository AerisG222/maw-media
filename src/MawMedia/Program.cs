using MawMedia.Authorization;
using MawMedia.Extensions;
using MawMedia.LocationCorrectionWorker;
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
    .AddCustomApiVersioning()
    .AddCustomOpenApi()
    .AddCustomAuth(builder.Configuration)
    .AddSingleton<IClock>(SystemClock.Instance)
    .AddMediaAuthorizationHandler()
    .AddLocationCorrectionWorker(builder.Configuration)
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

var versionSet = app.BuildVersionSet();

var api = app
    .MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(versionSet);

api.MapGroup("/auth").MapAuthRoutes();
api.MapGroup("/categories").MapCategoryRoutes();
api.MapGroup("/config").MapConfigRoutes();
api.MapGroup("/locations").MapLocationRoutes();
api.MapGroup("/media").MapMediaRoutes();
api.MapGroup("/stats").MapStatRoutes();
api.MapGroup("/upload").MapUploadRoutes();

await app.RunAsync();
