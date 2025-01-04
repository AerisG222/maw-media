using Npgsql;

namespace MawApi.Extensions;

public static class NpgsqlExtensions
{
    public static IServiceCollection AddNpgsql(
        this IServiceCollection services,
        IConfiguration config
    ) {
        var connString = config["Npgsql:ConnectionString"];

        ArgumentException.ThrowIfNullOrWhiteSpace(connString);

        services
            .AddSingleton(services =>
                new NpgsqlDataSourceBuilder(connString)
                    .UseNodaTime()
                    .Build()
            );

        return services;
    }
}
