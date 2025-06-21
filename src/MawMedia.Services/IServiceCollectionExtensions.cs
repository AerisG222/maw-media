using Microsoft.Extensions.DependencyInjection;
using Dapper;
using MawMedia.Services.Models;

namespace MawMedia.Services;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMediaServices (
        this IServiceCollection services
    ) {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(InstantHandler.Default);
        //SqlMapper.AddTypeMap(typeof(string), System.Data.DbType.AnsiString);

        services
            .AddScoped<ICategoryRepository, CategoryRepository>()
            .AddScoped<IMediaRepository, MediaRepository>();

        return services;
    }
}
