using System.Text.Json;
using MawMedia.Services.Tests.Models;
using NodaTime;

namespace MawMedia.Services.Tests;

public static class Constants
{
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
    // ** end from db seed scripts **

    public static readonly DbLocation LOCATION_NY = new(
        Guid.CreateVersion7(),
        40.712776m,
        -74.005974m,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        "New York, NY, USA",
        "NY",
        "New York County",
        null,
        "USA",
        "New York",
        "Manhattan",
        null,
        null,
        "10007",
        null,
        null,
        "Broadway",
        "1",
        null
    );

    public static readonly DbLocation LOCATION_MA = new(
        Guid.CreateVersion7(),
        42.3555m,
        -71.0565m,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        "Boston, MA, USA",
        "MA",
        null,
        null,
        "USA",
        "Massachusetts",
        "Boston",
        null,
        null,
        "02109",
        null,
        null,
        null,
        null,
        null
    );

    public static readonly Guid ROLE_ADMIN = Guid.CreateVersion7();
    public static readonly Guid ROLE_FRIEND = Guid.CreateVersion7();

    public static readonly Guid USER_ADMIN = Guid.CreateVersion7();
    public static readonly Guid USER_JOHNDOE = Guid.CreateVersion7();

    public static readonly string EXTERNAL_ID_NOUSER = Guid.CreateVersion7().ToString();
    public static readonly string EXTERNAL_ID_USERADMIN = Guid.CreateVersion7().ToString();
    public static readonly string EXTERNAL_ID_JOHNDOE = Guid.CreateVersion7().ToString();

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
        new LocalDate(2022, 10, 20),
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        "nature"
    );

    public static readonly DbCategory CATEGORY_TRAVEL = new(
        Guid.CreateVersion7(),
        "Travel",
        new LocalDate(2023, 06, 20),
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        "travel"
    );

    public static readonly DbCategory CATEGORY_FOOD = new(
        Guid.CreateVersion7(),
        "Food",
        new LocalDate(2023, 06, 22),
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        "food"
    );

    public static readonly DbMedia MEDIA_NATURE_1 = new(
        Guid.CreateVersion7(),
        TYPE_PHOTO,
        LOCATION_NY.Id,
        null,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        GetTestMetadata("media_nature_1"),
        "media-nature-1"
    );

    public static readonly DbMedia MEDIA_NATURE_2 = new(
        Guid.CreateVersion7(),
        TYPE_PHOTO,
        LOCATION_NY.Id,
        null,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        GetTestMetadata("media_nature_2"),
        "media-nature-2"
    );

    public static readonly DbMedia MEDIA_TRAVEL_1 = new(
        Guid.CreateVersion7(),
        TYPE_PHOTO,
        LOCATION_NY.Id,
        null,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        GetTestMetadata("media_travel_1"),
        "media-travel-1"
    );

    public static readonly DbMedia MEDIA_FOOD_1 = new(
        Guid.CreateVersion7(),
        TYPE_PHOTO,
        LOCATION_NY.Id,
        null,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        GetTestMetadata("media_food_1"),
        "media-food-1"
    );

    public static readonly DbFile FILE_NATURE_1 = new(
        Guid.CreateVersion7(),
        MEDIA_NATURE_1.Id,
        TYPE_PHOTO,
        SCALE_FULL_HD,
        1920,
        1080,
        123456L,
        "/media/nature1.jpg"
    );

    public static readonly DbFile FILE_NATURE_2 = new(
        Guid.CreateVersion7(),
        MEDIA_NATURE_2.Id,
        TYPE_VIDEO,
        SCALE_FULL_HD,
        1920,
        1080,
        123456L,
        "/media/nature2.jpg"
    );

    public static readonly DbFile FILE_TRAVEL_1 = new(
        Guid.CreateVersion7(),
        MEDIA_TRAVEL_1.Id,
        TYPE_PHOTO,
        SCALE_FULL_HD,
        1920,
        1080,
        123456L,
        "/media/travel1.jpg"
    );

    // NO files for FOOD category to demonstrate those will not get pulled back when querying categories

    static JsonDocument GetTestMetadata(string name)
    {
        return JsonDocument.Parse(
            $$"""
            {
                "SourceFile": "{{name}}",
                "EXIF": {
                    "NAME": "{{name}}",
                    "Make": "Test Make",
                    "Model": "Test Model",
                    "ExposureTime": "1/100",
                    "FNumber": 2.8,
                    "ISOSpeedRatings": 100,
                    "FocalLength": "35 mm"
                }
            }
            """
        );
    }
}
