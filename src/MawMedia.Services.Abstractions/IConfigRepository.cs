using MawMedia.Models;

namespace MawMedia.Services;

public interface IConfigRepository
{
    Task<IEnumerable<Scale>> GetScales();
}
