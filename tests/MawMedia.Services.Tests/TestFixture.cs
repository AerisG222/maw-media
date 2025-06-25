using Dapper;
using Npgsql;
using MawMedia.Services.Models;
using CliWrap;

namespace MawMedia.Services.Tests;

public class TestFixture
    : IAsyncLifetime
{
    const string DIRNAME_MEDIA_TESTING = "media-testing";
    const string PGSQL_SVC_ACCT = "svc_maw_media";
    const int TEST_DB_PORT = 9876;

    public required NpgsqlDataSource DataSource { get; set; }

    public async ValueTask InitializeAsync()
    {
        Console.WriteLine("** BUILDING TEST ENVIRONMENT **");

        await BuildTestEnvironment();

        Console.WriteLine("** STARTING TESTS **");

        PrepareDataSource();
        ConfigureDapper();
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("** TESTS COMPLETED **");

        await DestroyTestEnvironment();

        Console.WriteLine("** DESTROYING TEST ENVIRONMENT **");
    }

    void ConfigureDapper()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(InstantHandler.Default);
    }

    void PrepareDataSource()
    {
        var connStr = BuildConnString();

        var builder = new NpgsqlDataSourceBuilder(connStr);
        builder.UseNodaTime();

        DataSource = builder.Build();
    }

    string BuildConnString()
    {
        var dir = GetPgsqlPwdDir();
        var pwdFile = Path.Combine(dir.FullName, "pgpwd", $"psql-{PGSQL_SVC_ACCT}");

        var pwd = File.ReadAllText(pwdFile).Trim();

        return $"Server=127.0.0.1;Port={TEST_DB_PORT};Database=maw_media;Max Auto Prepare=100;User Id={PGSQL_SVC_ACCT};Password={pwd}";
    }

    DirectoryInfo GetPgsqlPwdDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && dir.FullName.Length > 1)
        {
            var mediaTestingDir = new DirectoryInfo(Path.Combine(dir.FullName, DIRNAME_MEDIA_TESTING));

            if (mediaTestingDir.Exists)
            {
                return mediaTestingDir;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find media testing dir");
    }

    async Task BuildTestEnvironment()
    {
        await RunBashScript("test-up.sh");
    }

    async Task DestroyTestEnvironment()
    {
        await RunBashScript("test-down.sh");
    }

    async Task RunBashScript(string script)
    {
        await Cli
            .Wrap("bash")
            .WithArguments(args => args
                .Add(script)
            )
            .WithWorkingDirectory(".")
            .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(Console.WriteLine))
            .ExecuteAsync();
    }
}
