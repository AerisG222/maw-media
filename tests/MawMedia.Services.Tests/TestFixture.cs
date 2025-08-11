using CliWrap;
using Dapper;
using MawMedia.Services.Models;
using Npgsql;
using Xunit.Runner.Common;
using Xunit.Sdk;

namespace MawMedia.Services.Tests;

public class TestFixture
    : IAsyncLifetime
{
    const string DIRNAME_MEDIA_TESTING = "media-testing";
    const string PGSQL_ADMIN_ACCT = "postgres";
    const string PGSQL_SVC_ACCT = "svc_maw_media";
    const int TEST_DB_PORT = 9876;

    readonly IMessageSink _diagnosticMessageSink;

    public required NpgsqlDataSource DataSource { get; set; }

    public TestFixture(IMessageSink diagnosticMessageSink)
    {
        ArgumentNullException.ThrowIfNull(diagnosticMessageSink);

        _diagnosticMessageSink = diagnosticMessageSink;
    }

    public async ValueTask InitializeAsync()
    {
        ConfigureDapper();

        Log("** BUILDING TEST ENVIRONMENT **");

        await BuildTestEnvironment();

        var setupDataSource = PrepareDataSource(PGSQL_ADMIN_ACCT);

        Log("** SEEDING TEST ENVIRONMENT **");

        await SeedTestData(setupDataSource);

        Log("** STARTING TESTS **");

        DataSource = PrepareDataSource(PGSQL_SVC_ACCT);
    }

    public async ValueTask DisposeAsync()
    {
        Log("** TESTS COMPLETED **");

        await DestroyTestEnvironment();

        Log("** DESTROYING TEST ENVIRONMENT **");
    }

    async Task SeedTestData(NpgsqlDataSource dataSource)
    {
        var seeder = new DatabaseSeeder(dataSource);

        await seeder.PopuplateDatabase();
    }

    void ConfigureDapper()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(InstantHandler.Default);
        SqlMapper.AddTypeHandler(LocalDateHandler.Default);
    }

    NpgsqlDataSource PrepareDataSource(string username)
    {
        var connStr = BuildConnString(username);

        var builder = new NpgsqlDataSourceBuilder(connStr);
        builder.UseNodaTime();

        return builder.Build();
    }

    string BuildConnString(string username)
    {
        var dir = GetPgsqlPwdDir();
        var pwdFile = Path.Combine(dir.FullName, "pgpwd", $"psql-{username}");

        var pwd = File.ReadAllText(pwdFile).Trim();

        return $"Server=127.0.0.1;Port={TEST_DB_PORT};Database=maw_media;Max Auto Prepare=100;Include Error Detail=true;User Id={username};Password={pwd}";
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
            .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(Console.WriteLine))
            .ExecuteAsync();
    }

    void Log(string message)
    {
        _diagnosticMessageSink.OnMessage(new DiagnosticMessage(message));
    }
}
