using MawMedia.Models;
using NodaTime;

namespace MawMedia.Services.Abstractions;

public interface ICategoryRepository
{
    Task<IEnumerable<short>> GetCategoryYears(Guid userId, CancellationToken token = default);
    Task<IEnumerable<Category>> GetCategories(Guid userId, string baseUrl, short? year = null, CancellationToken token = default);
    Task<IEnumerable<Category>> GetCategoryUpdates(Guid userId, Instant date, string baseUrl, CancellationToken token = default);
    Task<Category?> GetCategory(Guid userId, Guid categoryId, string baseUrl, CancellationToken token = default);
    Task<IEnumerable<Media>> GetCategoryMedia(Guid userId, string baseUrl, Guid categoryId, CancellationToken token = default);
    Task<IEnumerable<Gps>> GetCategoryMediaGps(Guid userId, Guid categoryId, CancellationToken token = default);
    Task<IEnumerable<Guid>> GetCategoriesWithoutGps(Guid userId, short? year, CancellationToken token = default);
    Task<bool> SetIsFavorite(Guid userId, Guid categoryId, bool isFavorite, CancellationToken token = default);
    Task<bool> SetTeaserMedia(Guid userId, Guid categoryId, Guid mediaId, CancellationToken token = default);
    Task<SearchResult<Category>> Search(Guid userId, string baseUrl, string searchTerm, int offset, int limit, CancellationToken token = default);
}
