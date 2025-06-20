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
}
