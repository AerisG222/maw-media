using Microsoft.AspNetCore.DataProtection;

namespace MawMedia.Extensions;

public static class DataProtectionExtensions
{
    public static IServiceCollection AddCustomDataProtection(
        this IServiceCollection services,
        IConfiguration config
    ) {
        var dpPath = config["DataProtection:Path"];

        ArgumentException.ThrowIfNullOrWhiteSpace(dpPath);

        if(!Directory.Exists(dpPath))
        {
            throw new DirectoryNotFoundException(dpPath);
        }

        Console.WriteLine($"Using data protection directory: {dpPath}");

        if (!string.IsNullOrWhiteSpace(dpPath))
        {
            services
                .AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(dpPath));
        }

        return services;
    }
}
