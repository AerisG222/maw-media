using MawMedia.Services.Tests.Models;
using Microsoft.Extensions.Logging.Testing;
using NodaTime;

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

    public static TheoryData<Guid, int, IEnumerable<Guid>> GetCategoriesData => new()
    {
        { Guid.CreateVersion7(),  0, [] },
        { Constants.USER_ADMIN,   2, [Constants.CATEGORY_NATURE.Id, Constants.CATEGORY_TRAVEL.Id] },
        { Constants.USER_JOHNDOE, 1, [Constants.CATEGORY_TRAVEL.Id] }
    };

    [Theory]
    [MemberData(nameof(GetCategoriesData))]
    public async Task GetCategories(Guid userId, int expectedCount, IEnumerable<Guid> expectedIds)
    {
        var repo = GetRepo();

        var cats = await repo.GetCategories(userId);

        Assert.NotNull(cats);
        Assert.Equal(expectedCount, cats.Count());
        Assert.All(expectedIds, id => cats.Select(c => c.Id).Contains(id));
    }

    public static TheoryData<Guid, short, int, IEnumerable<Guid>> GetCategoriesByYearData => new()
    {
        { Guid.CreateVersion7(),  2022, 0, [] },
        { Constants.USER_ADMIN,   2022, 1, [Constants.CATEGORY_NATURE.Id] },
        { Constants.USER_JOHNDOE, 2022, 0, [] }
    };

    [Theory]
    [MemberData(nameof(GetCategoriesByYearData))]
    public async Task GetCategoriesByYear(Guid userId, short year, int expectedCount, IEnumerable<Guid> expectedIds)
    {
        var repo = GetRepo();

        var cats = await repo.GetCategories(userId, year);

        Assert.NotNull(cats);
        Assert.Equal(expectedCount, cats.Count());
        Assert.All(expectedIds, id => cats.Select(c => c.Id).Contains(id));
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

    public static TheoryData<Guid, Guid, int> GetCategoryMediaData => new()
    {
        { Guid.CreateVersion7(),  Guid.CreateVersion7(),        0 },
        { Guid.CreateVersion7(),  Constants.CATEGORY_NATURE.Id, 0 },
        { Constants.USER_ADMIN,   Guid.CreateVersion7(),        0 },
        { Constants.USER_ADMIN,   Constants.CATEGORY_NATURE.Id, 2 },
        { Constants.USER_ADMIN,   Constants.CATEGORY_TRAVEL.Id, 1 },
        { Constants.USER_ADMIN,   Constants.CATEGORY_FOOD.Id,   0 },  // no files, so not media not returned
        { Constants.USER_JOHNDOE, Constants.CATEGORY_NATURE.Id, 0 },
        { Constants.USER_JOHNDOE, Constants.CATEGORY_TRAVEL.Id, 1 },
        { Constants.USER_JOHNDOE, Constants.CATEGORY_FOOD.Id,   0 },
    };

    [Theory]
    [MemberData(nameof(GetCategoryMediaData))]
    public async Task GetCategoryMedia(Guid userId, Guid categoryId, int expectedCount)
    {
        var repo = GetRepo();

        var media = await repo.GetCategoryMedia(userId, categoryId);

        Assert.NotNull(media);
        Assert.Equal(expectedCount, media.Count());
    }

    [Fact]
    public async Task UpdateCategoryTeaser_Then_ObserveChategoryUpdated()
    {
        var repo = GetRepo();
        var startOfTest = Instant.FromDateTimeUtc(DateTime.UtcNow);

        var updatedCategory = await repo.SetTeaserMedia(Constants.USER_ADMIN, Constants.CATEGORY_NATURE.Id, Constants.MEDIA_NATURE_2);

        Assert.NotNull(updatedCategory);
        Assert.Equal(Constants.CATEGORY_NATURE.Id, updatedCategory.Id);
        Assert.Equal(Constants.MEDIA_NATURE_2, updatedCategory.Teaser.Id);

        var updates = await repo.GetCategoryUpdates(Constants.USER_ADMIN, startOfTest);

        Assert.NotNull(updates);
        Assert.NotEmpty(updates);
        Assert.Contains(updates, x => x.Teaser.Id == Constants.MEDIA_NATURE_2);
    }

    CategoryRepository GetRepo()
    {
        return new CategoryRepository(
            new FakeLogger<CategoryRepository>(),
            _fixture.DataSource.CreateConnection()
        );
    }
}
