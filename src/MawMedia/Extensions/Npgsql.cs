using Npgsql;

namespace MawMedia.Extensions;

public static class NpgsqlExtensions
{
    public static IServiceCollection AddNpgsql(
        this IServiceCollection services,
        IConfiguration config
    ) {
        var connString = config["Npgsql:ConnectionString"];

        ArgumentException.ThrowIfNullOrWhiteSpace(connString);

        services
            .AddNpgsqlDataSource(
                connString,
                builder => builder
                    .UseNodaTime()
                    .Build()
            );

        return services;
    }
}
