using Asp.Versioning;
using Asp.Versioning.Builder;

namespace MawMedia.Extensions;

public static class ApiVersioningExtensions
{
    // the single source of truth for the versions this API exposes.
    // to introduce v2: add it here, add `.HasApiVersion(V2)` to the set below,
    // and tag the v2 endpoints with `.MapToApiVersion(V2)`.
    public static readonly ApiVersion V1 = new(1, 0);

    public static readonly IReadOnlyList<ApiVersion> All = [V1];

    // matches the ApiExplorer GroupNameFormat ("'v'VVV") so the OpenAPI
    // document name lines up with the endpoint group name.
    public static string DocumentName(ApiVersion version) =>
        $"v{version.MajorVersion}";

    public static IServiceCollection AddCustomApiVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(opts =>
            {
                opts.DefaultApiVersion = V1;
                opts.AssumeDefaultVersionWhenUnspecified = true;   // let the SPA omit the segment during migration
                opts.ReportApiVersions = true;                      // emit api-supported-versions / api-deprecated-versions headers
                opts.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(opts =>
            {
                opts.GroupNameFormat = "'v'VVV";
                opts.SubstituteApiVersionInUrl = true;              // render /api/v1/media instead of /api/v{version}/media
            });

        return services;
    }

    public static ApiVersionSet BuildVersionSet(this IEndpointRouteBuilder app)
    {
        var builder = app.NewApiVersionSet();

        foreach (var version in All)
        {
            builder.HasApiVersion(version);
        }

        return builder
            .ReportApiVersions()
            .Build();
    }
}
