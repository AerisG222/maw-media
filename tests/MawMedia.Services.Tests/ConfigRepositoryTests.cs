using Microsoft.Extensions.Logging.Testing;

namespace MawMedia.Services.Tests;

public class ConfigRepositoryTests
{
    readonly TestFixture _fixture;

    public ConfigRepositoryTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task GetScales()
    {
        var repo = GetRepo();

        var result = await repo.GetScales();

        Assert.NotNull(result);
        Assert.Contains("qqvg", result.Select(s => s.Code));
        Assert.Contains("qqvg-fill", result.Select(s => s.Code));
        Assert.Contains("full", result.Select(s => s.Code));
    }

    ConfigRepository GetRepo()
    {
        return new ConfigRepository(
            new FakeLogger<ConfigRepository>(),
            _fixture.DataSource.CreateConnection()
        );
    }
}
