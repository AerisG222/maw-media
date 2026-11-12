namespace MawMedia.ViewModels;

public sealed record BulkMediaRolesRequest(
    Guid[] MediaIds,
    string[] Roles
);
