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

    // the browser origin the api is configured to trust.  without an origin
    // configured the cors middleware disables itself entirely, which is what let
    // a missing verb ship unnoticed - every test here speaks to the api directly
    // and never sends an Origin header, so nothing preflighted anything.
    public const string ORIGIN = "https://test-photos.mikeandwan.us";

    static readonly Lock _envLock = new();

    readonly string _scratchDir;

    public ApiFactory(string connectionString)
    {
        _scratchDir = Path.Combine(Path.GetTempPath(), $"maw-media-api-tests-{Guid.CreateVersion7()}");

        var dataProtection = CreateDir("data-protection");

        lock (_envLock)
        {
            Set("Npgsql__ConnectionString", connectionString);
            Set("CorsOriginUrls__0", ORIGIN);
            Set("OAuth__Authority", "https://test-login.mikeandwan.us");
            Set("OAuth__Audience", AUDIENCE);
            Set("DataProtection__Path", dataProtection);
            Set("Assets__RootDirectory", SharedAssetRoot.Value);
            Set("Faces__RootDirectory", CreateDir("faces"));
            // a sibling of the asset root, never inside it - PlaceCoverStore refuses
            // to start when the two overlap, because that branch is served
            // unauthenticated
            Set("PlaceCovers__RootDirectory", SharedCoverRoot.Value);
            Set("Upload__RootDirectory", CreateDir("upload"));
            Set("CategoryDownload__RootDirectory", CreateDir("download"));
            Set("CategoryDownload__CleanIntervalInMinutes", "60");
            Set("CategoryDownload__MinAgeBeforeDeleteInMinutes", "60");
            Set("LocationCorrection__UserId", Guid.Empty.ToString());
        }
    }

    // the asset and cover roots are shared by every factory, unlike the per-test
    // scratch directories above, and they have to be.
    //
    // these settings reach the host as environment variables, which are global to
    // the process: with tests running 32 wide, one factory can overwrite
    // Assets__RootDirectory between another factory writing it and that other
    // factory's host reading it.  every read path composes urls from the stored
    // path without touching a file, so this never mattered before - publishing a
    // cover is the first operation that actually opens one, and it would fail
    // intermittently against a directory belonging to a different test.
    //
    // sharing removes the race rather than narrowing it: whichever value wins,
    // every host sees the same tree.  the asset tree is read-only fixture data,
    // and published covers are named with fresh guids, so neither can collide.
    static readonly Lazy<string> SharedAssetRoot = new(MaterializeAssets);
    static readonly Lazy<string> SharedCoverRoot = new(() => SharedDir("place-covers"));

    // a stub rather than a real avif - nothing in this system decodes a cover, it
    // is copied byte for byte.  the body is derived from the path so every
    // rendition differs, which is what lets a test prove *which* one was published
    // rather than merely that something was.
    public static byte[] StubRendition(string path) =>
        [.. new byte[] { 0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70 },
           .. System.Text.Encoding.UTF8.GetBytes(path)];

    static string SharedDir(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "maw-media-api-tests-shared", name);

        Directory.CreateDirectory(path);

        return path;
    }

    // the seeder writes media.file rows, but nothing puts bytes on disk - which is
    // fine for every read path, because they only ever compose urls from the
    // stored path.  publishing a place cover is the one operation that actually
    // opens the file, so the fixture renditions are materialized here.
    //
    // the contents are a stub rather than a real avif.  nothing in this system
    // decodes a cover: it is copied byte for byte, which is exactly why the
    // originals must never be the source - see media.get_place_cover_candidate.
    static string MaterializeAssets()
    {
        var root = SharedDir("assets");

        foreach (var path in new[]
        {
            Constants.FILE_NATURE_1.Path,
            Constants.FILE_NATURE_2.Path,
            Constants.FILE_TRAVEL_1.Path,
            Constants.FILE_PLACE_MA.Path,
            Constants.FILE_PLACE_OVERRIDE.Path,
            Constants.FILE_PLACE_UK.Path,
            Constants.FILE_NATURE_1_COVER.Path,
            Constants.FILE_TRAVEL_1_COVER.Path,
            Constants.FILE_PLACE_MA_COVER.Path,
            Constants.FILE_PLACE_OVERRIDE_COVER.Path
        })
        {
            var file = Path.Combine(root, path.TrimStart('/'));

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);

            File.WriteAllBytes(file, StubRendition(path));
        }

        return root;
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
