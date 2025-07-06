using System.Text.Json;
using MawMedia.Models;

namespace MawMedia.Services;

public interface IMediaRepository
{
    Task<IEnumerable<Media>> GetRandomMedia(Guid userId, byte count);
    Task<Media?> GetMedia(Guid userId, Guid mediaId);
    Task<Gps?> GetGps(Guid userId, Guid mediaId);
    Task<JsonDocument?> GetMetadata(Guid userId, Guid mediaId);
    Task<Media?> SetIsFavorite(Guid userId, Guid mediaId, bool isFavorite);
    Task<IEnumerable<Comment>> GetComments(Guid userId, Guid mediaId);
    Task<Guid?> AddComment(Guid userId, Guid mediaId, string body);
    Task<MediaFile?> GetMediaFile(Guid userId, Guid assetId);
}
