using MawMedia.Services.Tests.Models;
using Microsoft.Extensions.Logging.Testing;
using NodaTime;

namespace MawMedia.Services.Tests;

public class CategoryRepositoryTests
{
    const string BASE_URL = "https://media.example.com/";

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

        var cats = await repo.GetCategories(userId, BASE_URL);

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

        var cats = await repo.GetCategories(userId, BASE_URL, year);

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

        var cat = await repo.GetCategory(userId, categoryId, BASE_URL);

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
            Assert.True(Duration.FromMilliseconds(1) > (expected.Modified - cat.Modified));
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

        var media = await repo.GetCategoryMedia(userId, "http://example.com", categoryId);

        Assert.NotNull(media);
        Assert.Equal(expectedCount, media.Count());
    }

    public static TheoryData<Guid, Guid, Guid, bool> UpdateCategoryTeaserData => new()
    {
        { Guid.CreateVersion7(),  Guid.CreateVersion7(),        Constants.MEDIA_NATURE_2.Id, true  },
        { Guid.CreateVersion7(),  Constants.CATEGORY_NATURE.Id, Constants.MEDIA_NATURE_2.Id, true  },
        { Constants.USER_ADMIN,   Guid.CreateVersion7(),        Constants.MEDIA_NATURE_2.Id, true  },
        { Constants.USER_ADMIN,   Constants.CATEGORY_NATURE.Id, Constants.MEDIA_NATURE_2.Id, false },
        { Constants.USER_JOHNDOE, Constants.CATEGORY_NATURE.Id, Constants.MEDIA_NATURE_2.Id, true  }, // no access to category
        { Constants.USER_JOHNDOE, Constants.CATEGORY_TRAVEL.Id, Constants.MEDIA_TRAVEL_1.Id, true  }, // not category owner
    };

    [Theory]
    [MemberData(nameof(UpdateCategoryTeaserData))]
    public async Task UpdateCategoryTeaser(Guid userId, Guid categoryId, Guid mediaId, bool shouldFail)
    {
        var repo = GetRepo();

        var success = await repo.SetTeaserMedia(userId, categoryId, mediaId);

        if (shouldFail)
        {
            Assert.False(success);
        }
        else
        {
            var updatedCategory =  await repo.GetCategory(userId, categoryId, BASE_URL);

            Assert.NotNull(updatedCategory);
            Assert.Equal(Constants.CATEGORY_NATURE.Id, updatedCategory.Id);
            Assert.Equal(Constants.MEDIA_NATURE_2.Id, updatedCategory.Teaser.Id);
        }
    }

    [Fact]
    public async Task UpdateCategoryTeaser_Then_ObserveChanges()
    {
        var repo = GetRepo();
        var startOfTest = Instant.FromDateTimeUtc(DateTime.UtcNow);

        var success = await repo.SetTeaserMedia(Constants.USER_ADMIN, Constants.CATEGORY_NATURE.Id, Constants.MEDIA_NATURE_2.Id);

        Assert.True(success);

        var updatedCategory =  await repo.GetCategory(Constants.USER_ADMIN, Constants.CATEGORY_NATURE.Id, BASE_URL);

        Assert.NotNull(updatedCategory);
        Assert.Equal(Constants.CATEGORY_NATURE.Id, updatedCategory.Id);
        Assert.Equal(Constants.MEDIA_NATURE_2.Id, updatedCategory.Teaser.Id);

        var updates = await repo.GetCategoryUpdates(Constants.USER_ADMIN, startOfTest, BASE_URL);

        Assert.NotNull(updates);
        Assert.NotEmpty(updates);
        Assert.Contains(updates, x => x.Teaser.Id == Constants.MEDIA_NATURE_2.Id);
    }

    public static TheoryData<Guid, Guid, bool, bool> FavoriteCategoryData => new()
    {
        { Guid.CreateVersion7(), Guid.CreateVersion7(),        true,  true  },
        { Guid.CreateVersion7(), Guid.CreateVersion7(),        true,  false },
        { Guid.CreateVersion7(), Constants.CATEGORY_NATURE.Id, true,  true  },
        { Guid.CreateVersion7(), Constants.CATEGORY_NATURE.Id, true,  false },
        { Constants.USER_ADMIN,  Guid.CreateVersion7(),        true,  true  },
        { Constants.USER_ADMIN,  Guid.CreateVersion7(),        true,  false },
        { Constants.USER_ADMIN,  Constants.CATEGORY_NATURE.Id, false, true  },
        { Constants.USER_ADMIN,  Constants.CATEGORY_NATURE.Id, false, false }
    };

    [Theory]
    [MemberData(nameof(FavoriteCategoryData))]
    public async Task FavoriteCategory(Guid userId, Guid categoryId, bool shouldFail, bool doFavorite)
    {
        var repo = GetRepo();

        var success = await repo.SetIsFavorite(userId, categoryId, doFavorite);

        if (shouldFail)
        {
            Assert.False(success);
        }
        else
        {
            Assert.True(success);

            var updatedCategory =  await repo.GetCategory(userId, categoryId, BASE_URL);

            Assert.NotNull(updatedCategory);
            Assert.Equal(Constants.CATEGORY_NATURE.Id, updatedCategory.Id);
            Assert.Equal(doFavorite, updatedCategory.IsFavorite);
        }
    }

    public static TheoryData<Guid, Guid, int> GetCategoryMediaGpsData => new()
    {
        { Guid.CreateVersion7(),  Guid.CreateVersion7(),        0 },
        { Guid.CreateVersion7(),  Constants.CATEGORY_NATURE.Id, 0 },
        { Constants.USER_ADMIN,   Guid.CreateVersion7(),        0 },
        { Constants.USER_ADMIN,   Constants.CATEGORY_NATURE.Id, 2 },
        { Constants.USER_JOHNDOE, Constants.CATEGORY_NATURE.Id, 0 }
    };

    [Theory]
    [MemberData(nameof(GetCategoryMediaGpsData))]
    public async Task GetCategoryMediaGps(Guid userId, Guid categoryId, int expectedCount)
    {
        var repo = GetRepo();

        var gps = await repo.GetCategoryMediaGps(userId, categoryId);

        Assert.NotNull(gps);
        Assert.Equal(expectedCount, gps.Count());
    }

    public static TheoryData<Guid, string, IEnumerable<Guid>> SearchCategoriesData => new()
    {
        { Guid.CreateVersion7(),  "Nature", [] },
        { Constants.USER_ADMIN,   "Nature", [Constants.CATEGORY_NATURE.Id] },
        { Constants.USER_JOHNDOE, "Nature", [] }
    };

    [Theory]
    [MemberData(nameof(SearchCategoriesData))]
    public async Task SearchCategories(Guid userId, string searchTerm, IEnumerable<Guid> expectedIds)
    {
        var repo = GetRepo();

        var result = await repo.Search(userId, BASE_URL, searchTerm, 0, 100);

        Assert.NotNull(result);
        Assert.Equal(expectedIds.Count(), result.Results.Count());
        Assert.All(expectedIds, id => result.Results.Select(c => c.Id).Contains(id));
    }

    CategoryRepository GetRepo()
    {
        return new CategoryRepository(
            new FakeLogger<CategoryRepository>(),
            _fixture.DataSource.CreateConnection(),
            new FakeHybridCache(),
            new AssetPathBuilder()
        );
    }
}
