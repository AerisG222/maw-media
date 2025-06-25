using Dapper;
using Npgsql;
using MawMedia.Services.Models;
using CliWrap;
using Xunit.Sdk;
using Xunit.Runner.Common;

namespace MawMedia.Services.Tests;

public class TestFixture
    : IAsyncLifetime
{
    const string DIRNAME_MEDIA_TESTING = "media-testing";
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
        Log("** BUILDING TEST ENVIRONMENT **");

        await BuildTestEnvironment();

        Log("** STARTING TESTS **");

        PrepareDataSource();
        ConfigureDapper();
    }

    public async ValueTask DisposeAsync()
    {
        Log("** TESTS COMPLETED **");

        await DestroyTestEnvironment();

        Log("** DESTROYING TEST ENVIRONMENT **");
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
            .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(Console.WriteLine))
            .ExecuteAsync();
    }

    void Log(string message)
    {
        _diagnosticMessageSink.OnMessage(new DiagnosticMessage(message));
    }
}
