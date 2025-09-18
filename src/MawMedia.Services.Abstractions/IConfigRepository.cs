using MawMedia.Models;

namespace MawMedia.Services;

public interface IConfigRepository
{
    Task<IEnumerable<Scale>> GetScales();
    Task<bool> GetIsAdmin(Guid userId);
}
