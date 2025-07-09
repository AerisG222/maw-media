using Microsoft.Extensions.DependencyInjection;
using Dapper;
using MawMedia.Services.Models;
using Microsoft.Extensions.Configuration;

namespace MawMedia.Services;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMediaServices (
        this IServiceCollection services,
        IConfiguration configuration
    ) {
        ArgumentNullException.ThrowIfNull(configuration);

        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(InstantHandler.Default);
        //SqlMapper.AddTypeMap(typeof(string), System.Data.DbType.AnsiString);

        services
            .Configure<AssetConfig>(configuration.GetSection("Assets"))
            .Configure<CategoryDownloadConfig>(configuration.GetSection("CategoryDownload"))
            .AddScoped<ICategoryRepository, CategoryRepository>()
            .AddScoped<IMediaRepository, MediaRepository>()
            .AddScoped<IStatRepository, StatRepository>()
            .AddSingleton<IZipFileWriter, CategoryZipFileWriter>();

        return services;
    }
}
