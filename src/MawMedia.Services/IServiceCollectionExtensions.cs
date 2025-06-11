using Microsoft.Extensions.DependencyInjection;
using Dapper;

namespace MawMedia.Services;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMediaServices (
        this IServiceCollection services
    ) {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        //SqlMapper.AddTypeMap(typeof(string), System.Data.DbType.AnsiString);

        services
            .AddScoped<ICategoryRepository, CategoryRepository>();

        return services;
    }
}
