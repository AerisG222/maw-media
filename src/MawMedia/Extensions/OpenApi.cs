using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.OpenApi.Models;

namespace MawMedia.Extensions;

public static class OpenApiExtensions
{
    // currently, the default openapi call does not recognize authorization requirements, so we add this ourselves
    // https://github.com/dotnet/aspnetcore/issues/39761
    // https://github.com/martincostello/aspnetcore-openapi/blob/d87b42a236762ac32d833e6b482500b4d97f118c/src/TodoApp/OpenApi/AspNetCore/AspNetCoreOpenApiEndpoints.cs#L35-L53
    public static IServiceCollection AddCustomOpenApi
    (
        this IServiceCollection services,
        string title,
        string description
    )
    {
        services
            .Configure<RouteOptions>(options => options.SetParameterPolicy<RegexInlineRouteConstraint>("regex"))
            .AddOpenApi(opts =>
            {
                opts.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info.Title = title;
                    document.Info.Description = description;

                    var scheme = new OpenApiSecurityScheme()
                    {
                        BearerFormat = "JSON Web Token",
                        Description = "Bearer authentication using a JWT.",
                        Scheme = "bearer",
                        Type = SecuritySchemeType.Http,
                        Reference = new()
                        {
                            Id = "Bearer",
                            Type = ReferenceType.SecurityScheme,
                        },
                    };

                    document.Components ??= new();
                    document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
                    document.Components.SecuritySchemes[scheme.Reference.Id] = scheme;
                    document.SecurityRequirements ??= [];
                    document.SecurityRequirements.Add(new() { [scheme] = [] });

                    return Task.CompletedTask;
                });
            })
            .AddEndpointsApiExplorer();

        return services;
    }
}
