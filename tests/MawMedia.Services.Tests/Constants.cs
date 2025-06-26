namespace MawMedia.Services.Tests;

public static class Constants
{
    public static readonly Guid ROLE_ADMIN = Guid.CreateVersion7();
    public static readonly Guid ROLE_FRIEND = Guid.CreateVersion7();

    public static readonly Guid USER_ADMIN = Guid.CreateVersion7();
    public static readonly Guid USER_JOHNDOE = Guid.CreateVersion7();

    public static readonly IEnumerable<dynamic> UserRoles = [
        new {
            user_id = USER_ADMIN,
            role_id = ROLE_ADMIN
        },
        new {
            user_id = USER_ADMIN,
            role_id = ROLE_FRIEND
        },
        new {
            user_id = USER_JOHNDOE,
            role_id = ROLE_FRIEND
        }
    ];
}
