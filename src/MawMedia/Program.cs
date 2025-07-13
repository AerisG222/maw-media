using NodaTime;
using ZiggyCreatures.Caching.Fusion;
using MawMedia.Authorization;
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
    .ConfigureCustomJsonOptions()
    .AddNpgsql(builder.Configuration)
    .AddFusionCache()
        .AsHybridCache()
        .Services
    .AddCustomOpenApi()
    .AddCustomAuth(builder.Configuration)
    .AddSingleton<IClock>(services => SystemClock.Instance)
    .AddMediaAuthorizationHandler()
    .AddMediaServices(builder.Configuration);

var app = builder.Build();

app
    .UseForwardedHeaders()
    .UseHttpsRedirection()
    .UseRouting()
    .UseCustomSecurityHeaders()
    .UseCors()
    .UseAuthentication()
    .UseAuthorization()
    .UseCustomStaticFiles()
    .UseCustomOpenApi();

app.MapGroup("/categories").MapCategoryRoutes();
app.MapGroup("/config").MapConfigRoutes();
app.MapGroup("/media").MapMediaRoutes();
app.MapGroup("/stats").MapStatRoutes();
app.MapGroup("/upload").MapUploadRoutes();

await app.RunAsync();
