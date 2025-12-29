using MawMedia.Models;

namespace MawMedia.Services;

public interface ILocationRepository
{
    Task<IEnumerable<Location>> GetLocationsWithoutMetadata(Guid userId);
    Task<bool> SetLocationMetadata(Guid userId, LocationMetadata metadata);
}
