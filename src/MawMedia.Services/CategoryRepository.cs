using MawMedia.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MawMedia.Services;

public class CategoryRepository
    : BaseRepository, ICategoryRepository
{
    public CategoryRepository(
        ILogger<CategoryRepository> log,
        NpgsqlConnection conn
    ) : base(log, conn)
    {

    }

    public async Task<IEnumerable<Category>> GetCategories(Guid userId)
    {
        return await Query<Category>(
            "SELECT * FROM media.get_categories(@userId);",
            new
            {
                userId
            }
        );
    }
}
