namespace MawMedia.ViewModels;

public record BulkClearMediaRolesRequest(
    Guid[] MediaIds
);
