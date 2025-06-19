using MawMedia.Models;

namespace MawMedia.Services;

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetCategories(Guid userId);
    Task<Category?> GetCategory(Guid userId, Guid categoryId);
    Task<IEnumerable<Media>> GetCategoryMedia(Guid userId, Guid categoryId);
}
