using Dapper;
using MawMedia.Services.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MawMedia.Services;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMediaServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(InstantHandler.Default);
        //SqlMapper.AddTypeMap(typeof(string), System.Data.DbType.AnsiString);

        services
            .Configure<AssetConfig>(configuration.GetSection("Assets"))
            .Configure<CategoryDownloadConfig>(configuration.GetSection("CategoryDownload"))
            .Configure<UploadConfig>(configuration.GetSection("Upload"))
            .AddScoped<ICategoryRepository, CategoryRepository>()
            .AddScoped<IConfigRepository, ConfigRepository>()
            .AddScoped<IMediaRepository, MediaRepository>()
            .AddScoped<IStatRepository, StatRepository>()
            .AddSingleton<IAssetPathBuilder, AssetPathBuilder>()
            .AddSingleton<IZipFileWriter, CategoryZipFileWriter>()
            .AddSingleton<IUploadService, UploadService>()
            .AddHostedService<CategoryDownloadCleaner>();

        return services;
    }
}
