using NodaTime;
using ZiggyCreatures.Caching.Fusion;
using MawMedia.Extensions;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration
    .AddEnvironmentVariables("MAW_MEDIA_");

#pragma warning disable EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
builder.Services
    .ConfigureDataProtection(builder.Configuration)
    .ConfigureForwardedHeaders()
    .AddSystemd()
    //.AddNpgsql(builder.Configuration)
    .AddSingleton<IClock>(services => SystemClock.Instance)
    .AddFusionCache()
        .AsHybridCache()
        .Services
    .AddOpenApi()
    .AddRouting();
#pragma warning restore EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app
    .UseDefaultSecurityHeaders()
    .UseForwardedHeaders()
    .UseHttpsRedirection();

app.Run();
