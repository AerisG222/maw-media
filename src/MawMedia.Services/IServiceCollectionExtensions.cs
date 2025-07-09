using Microsoft.Extensions.DependencyInjection;
using Dapper;
using MawMedia.Services.Models;

namespace MawMedia.Services;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMediaServices (
        this IServiceCollection services,
        string assetRootDirectory,
        string categoryDownloadRootDirectory
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryDownloadRootDirectory);

        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(InstantHandler.Default);
        //SqlMapper.AddTypeMap(typeof(string), System.Data.DbType.AnsiString);

        services
            .AddScoped<ICategoryRepository, CategoryRepository>()
            .AddScoped<IMediaRepository, MediaRepository>()
            .AddScoped<IStatRepository, StatRepository>()
            .AddSingleton<IZipFileWriter>(s => new CategoryZipFileWriter(
                assetRootDirectory,
                categoryDownloadRootDirectory)
            );

        return services;
    }
}
