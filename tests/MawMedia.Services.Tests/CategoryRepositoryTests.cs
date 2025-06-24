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

    CategoryRepository GetRepo()
    {
        return new CategoryRepository(
            new FakeLogger<CategoryRepository>(),
            _fixture.DataSource.CreateConnection()
        );
    }
}
