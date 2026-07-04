using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MawMedia.LocationCorrectionWorker;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLocationCorrectionWorker(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .Configure<LocationCorrectionConfig>(configuration.GetSection("LocationCorrection"))
            .AddScoped<ILocationFixer, LocationFixer>()
            .AddHostedService<LocationCorrectionWorker>();

        return services;
    }
}
