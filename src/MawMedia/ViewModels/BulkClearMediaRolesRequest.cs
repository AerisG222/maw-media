namespace MawMedia.ViewModels;

public sealed record BulkClearMediaRolesRequest(
    Guid[] MediaIds
);
