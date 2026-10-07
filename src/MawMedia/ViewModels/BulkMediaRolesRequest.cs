namespace MawMedia.ViewModels;

public record BulkMediaRolesRequest(
    Guid[] MediaIds,
    string[] Roles
);
