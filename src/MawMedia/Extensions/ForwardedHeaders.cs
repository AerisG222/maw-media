using Microsoft.AspNetCore.HttpOverrides;

namespace MawMedia.Extensions;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddCustomForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .Configure<ForwardedHeadersOptions>(opts =>
            {
                opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                var knownNetworks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>();

                if(knownNetworks == null || knownNetworks.Length == 0)
                {
                    Console.WriteLine("No KnownNetworks found when configuring ForwardedHeaders!");
                }
                else
                {
                    foreach(var network in knownNetworks)
                    {
                        opts.KnownNetworks.Add(IPNetwork.Parse(network));
                    }
                }
            });

        return services;
    }
}
