using MawMedia.Models;

namespace MawMedia.ViewModels;

public sealed record BulkUpdateGpsRequest(
    Guid[] MediaIds,
    GpsCoordinate GpsCoordinate
);
