using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace MawMedia.Extensions;

public static class OpenApiExtensions
{
    const string TITLE = "MaW Media API";
    const string DESCRIPTION = "An API to access photos and videos from media.mikeandwan.us.";

    // currently, the default openapi call does not recognize authorization requirements, so we add this ourselves
    // https://github.com/dotnet/aspnetcore/issues/39761
    // https://github.com/martincostello/aspnetcore-openapi/blob/d87b42a236762ac32d833e6b482500b4d97f118c/src/TodoApp/OpenApi/AspNetCore/AspNetCoreOpenApiEndpoints.cs#L35-L53
    public static IServiceCollection AddCustomOpenApi(this IServiceCollection services)
    {
        services
            .Configure<RouteOptions>(options => options.SetParameterPolicy<RegexInlineRouteConstraint>("regex"))
            .AddOpenApi(opts =>
            {
                opts.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info.Title = TITLE;
                    document.Info.Description = DESCRIPTION;

                    var scheme = new OpenApiSecurityScheme()
                    {
                        BearerFormat = "JSON Web Token",
                        Description = "Bearer authentication using a JWT.",
                        Scheme = "Bearer",
                        Type = SecuritySchemeType.Http
                    };

                    document.Components ??= new();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes[scheme.Scheme] = scheme;

                    return Task.CompletedTask;
                });
            })
            .AddEndpointsApiExplorer();

        return services;
    }

    public static IApplicationBuilder UseCustomOpenApi(this IApplicationBuilder app)
    {
        var webApp = (WebApplication)app;

        webApp.MapOpenApi();
        webApp.MapScalarApiReference(opts =>
        {
            opts.AddAuthorizationCodeFlow("OAuth2", flow => { });
        });

        return webApp;
    }
}
