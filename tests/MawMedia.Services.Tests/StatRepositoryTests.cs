using Microsoft.Extensions.Logging.Testing;

namespace MawMedia.Services.Tests;

public class StatRepositoryTests
{
    readonly TestFixture _fixture;

    public StatRepositoryTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    public static TheoryData<Guid, int> GetStatsData => new()
    {
        { Guid.CreateVersion7(),  2 },
        { Constants.USER_ADMIN,   2 },
        { Constants.USER_JOHNDOE, 2 }
    };

    [Theory]
    [MemberData(nameof(GetStatsData))]
    public async Task GetStats(Guid userId, int expectedCount)
    {
        var repo = GetRepo();

        var result = await repo.GetStats(userId);

        Assert.NotNull(result);
        Assert.Equal(expectedCount, result.Count());
    }

    public static TheoryData<Guid, short, int, string> GetStatsForYearData => new()
    {
        { Guid.CreateVersion7(),  2022, 1, "Other" },
        { Constants.USER_ADMIN,   2022, 1, Constants.CATEGORY_NATURE.Name },
        { Constants.USER_JOHNDOE, 2022, 1, "Other" }
    };

    [Theory]
    [MemberData(nameof(GetStatsForYearData))]
    public async Task GetStatsForYear(Guid userId, short year, int expectedCount, string expectedCategoryName)
    {
        var repo = GetRepo();

        var result = await repo.GetStatsForYear(userId, year);

        Assert.NotNull(result);
        Assert.Equal(expectedCount, result.Count());
        Assert.Equal(result.First().CategoryName, expectedCategoryName);
    }

    StatRepository GetRepo()
    {
        return new StatRepository(
            new FakeLogger<StatRepository>(),
            _fixture.DataSource.CreateConnection()
        );
    }
}
