using MawMedia.Models;

namespace MawMedia.ViewModels;

public record class BulkUpdateGpsRequest(
    Guid[] MediaIds,
    GpsCoordinate GpsCoordinate
);
