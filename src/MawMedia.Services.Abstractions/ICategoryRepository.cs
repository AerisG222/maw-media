using NodaTime;
using MawMedia.Models;

namespace MawMedia.Services;

public interface ICategoryRepository
{
    Task<IEnumerable<short>> GetCategoryYears(Guid userId);
    Task<IEnumerable<Category>> GetCategories(Guid userId, string baseUrl, short? year = null);
    Task<IEnumerable<Category>> GetCategoryUpdates(Guid userId, Instant date, string baseUrl);
    Task<Category?> GetCategory(Guid userId, Guid categoryId, string baseUrl);
    Task<IEnumerable<Media>> GetCategoryMedia(Guid userId, Guid categoryId);
    Task<IEnumerable<Gps>> GetCategoryMediaGps(Guid userId, Guid categoryId);
    Task<bool> SetIsFavorite(Guid userId, Guid categoryId, bool isFavorite);
    Task<bool> SetTeaserMedia(Guid userId, Guid categoryId, Guid mediaId);
    Task<SearchResult<Category>> Search(Guid userId, string baseUrl, string searchTerm, int offset, int limit);
}
