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
        "Suffolk County",
        null,
        "USA",
        "Boston",
        "North End",
        null,
        null,
        "02109",
        null,
        null,
        null,
        null,
        null
    );

    public static readonly DbLocation LOCATION_UNK = new(
        Guid.CreateVersion7(),
        41,
        -74,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
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

    // NOTE: declaration order matters here.  static fields initialize top to
    // bottom, so anything referencing USER_ADMIN, TYPE_PHOTO, SCALE_FULL_HD or a
    // LOCATION_* must be declared after them - a forward reference silently reads
    // Guid.Empty rather than failing to compile, and surfaces much later as a
    // foreign key violation during seeding.
    // browsing by location.  these reuse the existing categories rather than
    // adding new ones, which keeps them out of every test that counts categories,
    // years or search hits: CATEGORY_TRAVEL is already shared with ROLE_FRIEND and
    // CATEGORY_FOOD is already admin only, which is exactly the pair of visibility
    // rules these fixtures need.  they stay clear of CATEGORY_NATURE, which
    // CategoryRepositoryTests pins at two gps-bearing media.
    //
    // the derived tree they produce is:
    //
    //   USA            -> NY -> New York   (MEDIA_TRAVEL_1 etc, pre-existing)
    //                  -> MA -> Boston     (MEDIA_PLACE_MA, MEDIA_PLACE_OVERRIDE)
    //   United Kingdom -> England -> London (MEDIA_PLACE_UK, admin only)
    public static readonly DbLocation LOCATION_UK = new(
        Guid.CreateVersion7(),
        51.507400m,
        -0.127800m,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        "London, England, United Kingdom",
        "England",
        "Greater London",
        null,
        "United Kingdom",
        "London",
        null,
        null,
        null,
        "SW1A 1AA",
        null,
        null,
        null,
        null,
        null
    );

    public static readonly DbMedia MEDIA_PLACE_MA = new(
        Guid.CreateVersion7(),
        TYPE_PHOTO,
        LOCATION_MA.Id,
        null,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        GetTestMetadata("media_nature_1"),
        "media-place-ma"
    );

    // the override fixture, and the reason it is not optional: the phase 0 audit
    // found 1,471 production media carry both columns with *different* values, and
    // 60,472 are reachable only through the override.  this one is recorded at
    // LOCATION_NY and overridden to LOCATION_MA, so it must browse under Boston and
    // must not appear under New York.
    public static readonly DbMedia MEDIA_PLACE_OVERRIDE = new(
        Guid.CreateVersion7(),
        TYPE_PHOTO,
        LOCATION_NY.Id,
        LOCATION_MA.Id,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        GetTestMetadata("media_nature_1"),
        "media-place-override"
    );

    public static readonly DbMedia MEDIA_PLACE_UK = new(
        Guid.CreateVersion7(),
        TYPE_PHOTO,
        LOCATION_UK.Id,
        null,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        Instant.FromDateTimeUtc(DateTime.UtcNow),
        USER_ADMIN,
        GetTestMetadata("media_nature_1"),
        "media-place-uk"
    );

    // covers are always published from the qvg-fill rendition, so the media that
    // are chosen as one carry a second file at that scale beside their full-hd.
    // two renditions rather than one is also what makes the selection meaningful -
    // with a single candidate the scale filter would pass no matter what it did.
    //
    // MEDIA_PLACE_UK deliberately has no qvg-fill.  it is the fixture for a media
    // that exists, is visible, sits at the right place, and still cannot be a
    // cover.
    public static readonly DbFile FILE_NATURE_1_COVER = new(
        Guid.CreateVersion7(), MEDIA_NATURE_1.Id, TYPE_PHOTO, SCALE_QVG_FILL,
        320, 240, 4096L, "/media/nature1-qvg-fill.jpg");

    public static readonly DbFile FILE_TRAVEL_1_COVER = new(
        Guid.CreateVersion7(), MEDIA_TRAVEL_1.Id, TYPE_PHOTO, SCALE_QVG_FILL,
        320, 240, 4096L, "/media/travel1-qvg-fill.jpg");

    public static readonly DbFile FILE_PLACE_MA_COVER = new(
        Guid.CreateVersion7(), MEDIA_PLACE_MA.Id, TYPE_PHOTO, SCALE_QVG_FILL,
        320, 240, 4096L, "/media/place-ma-qvg-fill.jpg");

    public static readonly DbFile FILE_PLACE_OVERRIDE_COVER = new(
        Guid.CreateVersion7(), MEDIA_PLACE_OVERRIDE.Id, TYPE_PHOTO, SCALE_QVG_FILL,
        320, 240, 4096L, "/media/place-override-qvg-fill.jpg");

    // every place media needs a file: media.get_place_media inner joins
    // media_detail, and a category's teaser needs one to render as a tile
    public static readonly DbFile FILE_PLACE_MA = new(
        Guid.CreateVersion7(), MEDIA_PLACE_MA.Id, TYPE_PHOTO, SCALE_FULL_HD,
        1920, 1080, 123456L, "/media/place-ma.jpg");

    public static readonly DbFile FILE_PLACE_OVERRIDE = new(
        Guid.CreateVersion7(), MEDIA_PLACE_OVERRIDE.Id, TYPE_PHOTO, SCALE_FULL_HD,
        1920, 1080, 123456L, "/media/place-override.jpg");

    public static readonly DbFile FILE_PLACE_UK = new(
        Guid.CreateVersion7(), MEDIA_PLACE_UK.Id, TYPE_PHOTO, SCALE_FULL_HD,
        1920, 1080, 123456L, "/media/place-uk.jpg");

    // face recognition.  PERSON_SHARED appears in both a nature photo (admin only)
    // and the travel photo (admin + friend), while PERSON_PRIVATE appears only in
    // nature - so johndoe, who holds ROLE_FRIEND, must see exactly one of them.
    public static readonly Guid PERSON_SHARED = Guid.CreateVersion7();
    public static readonly Guid PERSON_PRIVATE = Guid.CreateVersion7();

    // exists so the favourite toggle has a person nothing else asserts on.
    // test classes run in parallel, so a test that writes a favourite for a
    // person another class reads would be a flake - this one is written to only
    // by PersonRoutesTests.
    public static readonly Guid PERSON_TOGGLE = Guid.CreateVersion7();

    public static readonly Guid FACE_SHARED_NATURE = Guid.CreateVersion7();
    public static readonly Guid FACE_SHARED_TRAVEL = Guid.CreateVersion7();
    public static readonly Guid FACE_PRIVATE_NATURE = Guid.CreateVersion7();
    public static readonly Guid FACE_TOGGLE_TRAVEL = Guid.CreateVersion7();

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
