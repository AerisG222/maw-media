using MawMedia.Services.Tests.Models;
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

    public static TheoryData<Guid, int> GetYearsData => new()
    {
        { Guid.CreateVersion7(),  0 },
        { Constants.USER_ADMIN,   2 },
        { Constants.USER_JOHNDOE, 1 }
    };

    [Theory]
    [MemberData(nameof(GetYearsData))]
    public async Task GetYears(Guid userId, int expectedCount)
    {
        var repo = GetRepo();

        var years = await repo.GetCategoryYears(userId);

        Assert.NotNull(years);
        Assert.Equal(expectedCount, years.Count());
    }

    public static TheoryData<Guid, Guid, DbCategory?> GetCategoryData => new()
    {
        { Guid.CreateVersion7(),  Guid.CreateVersion7(),        null },
        { Guid.CreateVersion7(),  Constants.CATEGORY_NATURE.Id, null },
        { Constants.USER_ADMIN,   Guid.CreateVersion7(),        null },
        { Constants.USER_ADMIN,   Constants.CATEGORY_NATURE.Id, Constants.CATEGORY_NATURE },
        { Constants.USER_JOHNDOE, Constants.CATEGORY_NATURE.Id, null }
    };

    [Theory]
    [MemberData(nameof(GetCategoryData))]
    public async Task GetCategory(Guid userId, Guid categoryId, DbCategory? expected)
    {
        var repo = GetRepo();

        var cat = await repo.GetCategory(userId, categoryId);

        if (expected == null)
        {
            Assert.Null(cat);
        }
        else
        {
            Assert.NotNull(cat);
            Assert.Equal(expected.Id, cat.Id);
            Assert.Equal(expected.Name, cat.Name);
            Assert.Equal(expected.EffectiveDate, cat.EffectiveDate);
            Assert.Equal(expected.Modified.ToString(), cat.Modified.ToString());
        }
    }

    [Fact]
    public async Task GetCategories_ForInvalidUser_ReturnsEmpty()
    {
        var repo = GetRepo();

        var cats = await repo.GetCategories(Guid.CreateVersion7());

        Assert.NotNull(cats);
        Assert.Empty(cats);
    }

    [Fact]
    public async Task GetCategories_ForAdmin_ReturnsBoth()
    {
        var repo = GetRepo();

        var cats = await repo.GetCategories(Constants.USER_ADMIN);

        Assert.NotNull(cats);
        Assert.Equal(2, cats.Count());  // excludes FOOD category
    }

    [Fact]
    public async Task GetCategories_ForRestrictedUser_ReturnsOne()
    {
        var repo = GetRepo();

        var cats = await repo.GetCategories(Constants.USER_JOHNDOE);

        Assert.NotNull(cats);
        Assert.Single(cats);
        Assert.Equal(Constants.CATEGORY_TRAVEL.Id, cats.First().Id);
    }

    [Fact]
    public async Task GetCategoryMedia_ForInvalidUserAndCategory_ReturnsEmpty()
    {
        var repo = GetRepo();

        var media = await repo.GetCategoryMedia(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.NotNull(media);
        Assert.Empty(media);
    }

    [Fact]
    public async Task GetCategoryMedia_ForInvalidUserAndValidCategory_ReturnsEmpty()
    {
        var repo = GetRepo();

        var media = await repo.GetCategoryMedia(Guid.CreateVersion7(), Constants.CATEGORY_NATURE.Id);

        Assert.NotNull(media);
        Assert.Empty(media);
    }

    [Fact]
    public async Task GetCategoryMedia_ForValidUserAndInvalidCategory_ReturnsEmpty()
    {
        var repo = GetRepo();

        var media = await repo.GetCategoryMedia(Constants.USER_ADMIN, Guid.CreateVersion7());

        Assert.NotNull(media);
        Assert.Empty(media);
    }

    [Fact]
    public async Task GetCategoryMedia_WhenUserDoesNotHaveAccess_ReturnsEmpty()
    {
        var repo = GetRepo();

        var cat = await repo.GetCategoryMedia(Constants.USER_JOHNDOE, Constants.CATEGORY_NATURE.Id);

        Assert.NotNull(cat);
        Assert.Empty(cat);
    }

    [Fact]
    public async Task GetCategoryMedia_WhenUserHasAccess_ReturnsMedia()
    {
        var repo = GetRepo();

        var cats = await repo.GetCategoryMedia(Constants.USER_JOHNDOE, Constants.CATEGORY_TRAVEL.Id);

        Assert.NotNull(cats);
        Assert.NotEmpty(cats);  // excludes FOOD category
    }

    CategoryRepository GetRepo()
    {
        return new CategoryRepository(
            new FakeLogger<CategoryRepository>(),
            _fixture.DataSource.CreateConnection()
        );
    }
}
