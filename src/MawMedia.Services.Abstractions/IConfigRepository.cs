using MawMedia.Models;

namespace MawMedia.Services.Abstractions;

public interface IConfigRepository
{
    Task<IEnumerable<Scale>> GetScales();
}
