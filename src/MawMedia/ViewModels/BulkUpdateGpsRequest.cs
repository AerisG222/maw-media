using MawMedia.Models;

namespace MawMedia.ViewModels;

public record BulkUpdateGpsRequest(
    Guid[] MediaIds,
    GpsCoordinate GpsCoordinate
);
