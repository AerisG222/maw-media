using MawMedia.Models;
using MawMedia.Services.Tests.Models;
using NodaTime;

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

    public static readonly DbCategory CATEGORY_NATURE = new(
        Guid.CreateVersion7(),
        "Nature",
        LocalDate.FromDateTime(DateTime.UtcNow),
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN
    );

    public static readonly DbCategory CATEGORY_TRAVEL = new(
        Guid.CreateVersion7(),
        "Travel",
        LocalDate.FromDateTime(DateTime.UtcNow),
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN
    );

    public static readonly Guid MEDIA_NATURE_1 = Guid.CreateVersion7();
    public static readonly Guid MEDIA_TRAVEL_1 = Guid.CreateVersion7();

    public static readonly Guid LOCATION_NY = Guid.CreateVersion7();

    // ** from db seed scripts **
    public static readonly Guid TYPE_PHOTO = Guid.Parse("01964f94-fa50-7846-b2e6-26d4609cc972");
    public static readonly Guid TYPE_VIDEO = Guid.Parse("01964f94-fa51-705b-b0e2-b4c668ac6fab");
    public static readonly Guid TYPE_VIDEO_POSTER = Guid.Parse("01964f94-fa51-705b-b0e2-b4c668ac6fcd");

    public static readonly Guid SCALE_QQVG = Guid.Parse("01965306-0786-7af9-b3eb-a4dd6ef83505");
    public static readonly Guid SCALE_QQVG_FILL = Guid.Parse("01965306-0786-7af9-b3eb-a4dd6ef83606");
    public static readonly Guid SCALE_QVG = Guid.Parse("01965306-49b3-7212-b55d-345e7195a3b0");
    public static readonly Guid SCALE_QVG_FILL = Guid.Parse("01965306-49b3-7212-b55d-345e7195a6b6");
    public static readonly Guid SCALE_NHD = Guid.Parse("01965306-6f04-739f-aea6-3b4022f1d2ce");
    public static readonly Guid SCALE_NHD_FILL = Guid.Parse("01965306-6f04-739f-aea6-3b4022f1d6c6");
    public static readonly Guid SCALE_FULL_HD = Guid.Parse("01965306-9387-7cb7-8945-1d626de296fa");
    public static readonly Guid SCALE_QHD = Guid.Parse("01965306-b398-70e8-9bd4-af9e0bb96c8b");
    public static readonly Guid SCALE_4K = Guid.Parse("01965306-d3b1-754a-abf1-be97f6b18a83");
    public static readonly Guid SCALE_5K = Guid.Parse("01965307-039f-732f-a768-c09584310119");
    public static readonly Guid SCALE_8K = Guid.Parse("01965307-20f9-7e01-955e-be53d178662d");
    public static readonly Guid SCALE_SRC = Guid.Parse("01965307-20f9-7e01-955e-be53d1786828");
}
