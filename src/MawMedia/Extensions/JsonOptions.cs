using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace MawMedia.Extensions;

public static class JsonOptionsExtensions
{
    public static IServiceCollection ConfigureCustomJsonOptions(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(opts =>
        {
            opts.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        });

        return services;
    }
}
