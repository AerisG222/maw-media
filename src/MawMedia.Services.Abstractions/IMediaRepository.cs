using System.Text.Json;
using MawMedia.Models;

namespace MawMedia.Services;

public interface IMediaRepository
{
    Task<IEnumerable<Media>> GetRandomMedia(Guid userId, string baseUrl, byte count);
    Task<Media?> GetMedia(Guid userId, string baseUrl, Guid mediaId);
    Task<Gps?> GetGps(Guid userId, Guid mediaId);
    Task<JsonDocument?> GetMetadata(Guid userId, Guid mediaId);
    Task<Media?> SetIsFavorite(Guid userId, string baseUrl, Guid mediaId, bool isFavorite);
    Task<IEnumerable<Comment>> GetComments(Guid userId, Guid mediaId);
    Task<Comment?> GetComment(Guid userId, Guid commentId);
    Task<Guid?> AddComment(Guid userId, Guid mediaId, string body);
    Task<MediaFile?> GetMediaFile(Guid userId, Guid assetId);
    Task<MediaFile?> GetMediaFile(Guid userId, string path);
    ValueTask<bool> AllowAccessToAsset(Guid userId, string path, CancellationToken token);
    Task<bool> SetGpsOverride(Guid userId, Guid mediaId, Guid newLocationId, decimal latitude, decimal longitude);
    Task<bool> BulkSetGpsOverride(Guid userId, Guid[] mediaIds, Guid newLocationId, decimal latitude, decimal longitude);
}
