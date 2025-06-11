namespace MawMedia.Extensions;

public static class ConditionalExtensions
{
    public static IServiceCollection When
    (
        this IServiceCollection services,
        bool condition,
        Action<IServiceCollection> action
    ) {
        if(condition) {
            action(services);
        }

        return services;
    }

    public static IApplicationBuilder When(
        this IApplicationBuilder app,
        bool condition,
        Action<WebApplication> action
    )
    {
        if (condition)
        {
            var webApp = (WebApplication)app;

            action(webApp);
        }

        return app;
    }
}
