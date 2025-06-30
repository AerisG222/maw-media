using NodaTime;
using MawMedia.Models;

namespace MawMedia.Services;

public interface ICategoryRepository
{
    Task<IEnumerable<short>> GetCategoryYears(Guid userId);
    Task<IEnumerable<Category>> GetCategories(Guid userId, short? year = null);
    Task<IEnumerable<Category>> GetCategoryUpdates(Guid userId, Instant date);
    Task<Category?> GetCategory(Guid userId, Guid categoryId);
    Task<IEnumerable<Media>> GetCategoryMedia(Guid userId, Guid categoryId);
    Task<IEnumerable<Gps>> GetCategoryMediaGps(Guid userId, Guid categoryId);
    Task<Category?> SetIsFavorite(Guid userId, Guid categoryId, bool isFavorite);
    Task<Category?> SetTeaserMedia(Guid userId, Guid categoryId, Guid mediaId);
    Task<SearchResult<Category>> Search(Guid userId, string searchTerm, int offset, int limit);
}
