using System.Text.Json;
using MawMedia.Models;

namespace MawMedia.Services.Abstractions;

public interface IMediaRepository
{
    Task<IEnumerable<Media>> GetRandomMedia(Guid userId, string baseUrl, byte count, CancellationToken token = default);
    Task<Media?> GetMedia(Guid userId, string baseUrl, Guid mediaId, CancellationToken token = default);
    Task<Gps?> GetGps(Guid userId, Guid mediaId, CancellationToken token = default);
    Task<JsonDocument?> GetMetadata(Guid userId, Guid mediaId, CancellationToken token = default);
    Task<Media?> SetIsFavorite(Guid userId, string baseUrl, Guid mediaId, bool isFavorite, CancellationToken token = default);
    Task<IEnumerable<Comment>> GetComments(Guid userId, Guid mediaId, CancellationToken token = default);
    Task<Comment?> GetComment(Guid userId, Guid commentId, CancellationToken token = default);
    Task<Guid?> AddComment(Guid userId, Guid mediaId, string body, CancellationToken token = default);
    Task<MediaFile?> GetMediaFile(Guid userId, Guid assetId, CancellationToken token = default);
    Task<MediaFile?> GetMediaFile(Guid userId, string path, CancellationToken token = default);
    ValueTask<bool> AllowAccessToAsset(Guid userId, string path, CancellationToken token);
    Task<bool> SetGpsOverride(Guid userId, Guid mediaId, Guid newLocationId, decimal latitude, decimal longitude, CancellationToken token = default);
    Task<bool> BulkSetGpsOverride(Guid userId, Guid[] mediaIds, Guid newLocationId, decimal latitude, decimal longitude, CancellationToken token = default);
}
