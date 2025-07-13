using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace MawMedia.Authorization;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMediaAuthorizationHandler (this IServiceCollection services)
    {
        services
            .AddScoped<IAuthorizationHandler, MediaStaticAssetAuthorizationHandler>();

        return services;
    }
}
