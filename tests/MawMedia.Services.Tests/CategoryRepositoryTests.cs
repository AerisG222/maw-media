using Microsoft.Extensions.Logging.Testing;

namespace MawMedia.Services.Tests;

public class CategoryRepositoryTests
{
    readonly TestFixture _fixture;

    public CategoryRepositoryTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public async Task GetCategory_InvalidUserAndCategory_ReturnsNull()
    {
        var repo = GetRepo();

        var cat = await repo.GetCategory(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.Null(cat);
    }

    [Fact]
    public async Task GetCategory_InvalidUser_ReturnsNull()
    {
        var repo = GetRepo();

        var cat = await repo.GetCategory(Guid.CreateVersion7(), Constants.CATEGORY_NATURE.Id);

        Assert.Null(cat);
    }

    [Fact]
    public async Task GetCategory_InvalidCategory_ReturnsNull()
    {
        var repo = GetRepo();

        var cat = await repo.GetCategory(Constants.USER_ADMIN, Guid.CreateVersion7());

        Assert.Null(cat);
    }

    [Fact]
    public async Task GetCategory_WhenUserHasAccess_ReturnsCategory()
    {
        var repo = GetRepo();

        var cat = await repo.GetCategory(Constants.USER_ADMIN, Constants.CATEGORY_NATURE.Id);

        Assert.NotNull(cat);
        Assert.Equal(Constants.CATEGORY_NATURE.Id, cat.Id);
        Assert.Equal(Constants.CATEGORY_NATURE.Name, cat.Name);
        Assert.Equal(Constants.CATEGORY_NATURE.EffectiveDate, cat.EffectiveDate);

        // microsecond precision can be lost w/ nodatime, which is fine for us - use tostring as proxy for equality
        Assert.Equal(Constants.CATEGORY_NATURE.Modified.ToString(), cat.Modified.ToString());
    }

    CategoryRepository GetRepo()
    {
        return new CategoryRepository(
            new FakeLogger<CategoryRepository>(),
            _fixture.DataSource.CreateConnection()
        );
    }
}
