namespace MawMedia.ViewModels;

public sealed record BulkClearGpsRequest(
    Guid[] MediaIds
);
