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
    .AddCustomOAuthConfig(builder.Configuration)
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
    .UseUnversionedApiCompatibility()   // must precede UseRouting so the rewritten path is the one matched
    .UseRouting()
    .UseCustomSecurityHeaders(app.Environment)
    .UseCors()
    .UseAuthentication()
    .UseAuthorization()
    .UseCustomStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseCustomOpenApi();
}

var versionSet = app.BuildVersionSet();

var api = app
    .MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(versionSet)
    .RequireAuthorization();   // safety net now that no fallback policy applies; each route adds its own policy on top

api.MapGroup("/auth").MapAuthRoutes();
api.MapGroup("/categories").MapCategoryRoutes();
api.MapGroup("/config").MapConfigRoutes();
api.MapGroup("/faces").MapFaceRoutes();
api.MapGroup("/locations").MapLocationRoutes();
api.MapGroup("/media").MapMediaRoutes();
api.MapGroup("/persons").MapPersonRoutes();
api.MapGroup("/stats").MapStatRoutes();
api.MapGroup("/upload").MapUploadRoutes();

await app.RunAsync();

// top level statements compile to an internal Program, which
// WebApplicationFactory<Program> cannot reference.  this makes it public
// without otherwise changing the entry point.
public partial class Program;
