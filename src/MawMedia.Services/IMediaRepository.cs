using MawMedia.Models;

namespace MawMedia.Services;

public interface IMediaRepository
{
    Task<IEnumerable<Media>> GetRandomMedia(Guid userId, byte count);
}
