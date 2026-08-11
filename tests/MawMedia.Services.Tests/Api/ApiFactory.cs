using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MawMedia.Services.Tests.Api;

// hosts the real api over TestServer.  no kestrel means no certificate, which is
// what makes this workable without the production dev secrets.
//
// settings arrive as environment variables rather than through
// ConfigureAppConfiguration: Program.cs reads builder.Configuration while it is
// still registering services, which happens before the factory's configuration
// callbacks are applied, so an in-memory source would be too late to be seen by
// AddCustomDataProtection and friends.
public class ApiFactory
    : WebApplicationFactory<Program>
{
    public const string AUDIENCE = "https://test-media.mikeandwan.us";

    static readonly Lock _envLock = new();

    readonly string _scratchDir;

    public ApiFactory(string connectionString)
    {
        _scratchDir = Path.Combine(Path.GetTempPath(), $"maw-media-api-tests-{Guid.CreateVersion7()}");

        var dataProtection = CreateDir("data-protection");

        lock (_envLock)
        {
            Set("Npgsql__ConnectionString", connectionString);
            Set("OAuth__Authority", "https://test-login.mikeandwan.us");
            Set("OAuth__Audience", AUDIENCE);
            Set("DataProtection__Path", dataProtection);
            Set("Assets__RootDirectory", CreateDir("assets"));
            Set("Upload__RootDirectory", CreateDir("upload"));
            Set("CategoryDownload__RootDirectory", CreateDir("download"));
            Set("CategoryDownload__CleanIntervalInMinutes", "60");
            Set("CategoryDownload__MinAgeBeforeDeleteInMinutes", "60");
            Set("LocationCorrection__UserId", Guid.Empty.ToString());
        }
    }

    // the scope value the api will demand, qualified the way Auth0 does it
    public static string QualifiedScope(string scope) => $"{AUDIENCE}/{scope}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // background workers have nothing to do here, and the location
            // corrector would immediately start polling
            services.RemoveAll<IHostedService>();

            services
                .AddAuthentication(TestAuthHandler.SCHEME)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SCHEME, _ => { });

            // AddCustomAuth already pointed the defaults at JwtBearer, so they are
            // reset explicitly rather than relying on registration order
            services.Configure<AuthenticationOptions>(opts =>
            {
                opts.DefaultScheme = TestAuthHandler.SCHEME;
                opts.DefaultAuthenticateScheme = TestAuthHandler.SCHEME;
                opts.DefaultChallengeScheme = TestAuthHandler.SCHEME;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_scratchDir))
        {
            Directory.Delete(_scratchDir, true);
        }
    }

    static void Set(string key, string value) => Environment.SetEnvironmentVariable(key, value);

    string CreateDir(string name)
    {
        var path = Path.Combine(_scratchDir, name);

        Directory.CreateDirectory(path);

        return path;
    }
}
