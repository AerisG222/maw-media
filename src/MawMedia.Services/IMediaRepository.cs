using MawMedia.Models;

namespace MawMedia.Services;

public interface IMediaRepository
{
    Task<IEnumerable<Media>> GetRandomMedia(Guid userId, byte count);
    Task<Media?> GetMedia(Guid userId, Guid mediaId);
    Task<Media?> SetIsFavorite(Guid userId, Guid mediaId, bool isFavorite);
}
