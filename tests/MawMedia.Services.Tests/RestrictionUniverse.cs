using Dapper;

namespace MawMedia.Services.Tests;

// a private universe for testing media restrictions: its own roles, users,
// category, media, person and place, built per test.
//
// no seeded user holds these roles or can see this category, so nothing here is
// visible to the rest of the suite, and no count asserted anywhere else can move
// while these tests run in parallel with it.  the one seeded thing it touches is
// the role named 'admin', which the private admin also holds so media.get_is_admin
// answers true for them - and that role is not granted this category, so it
// changes nothing about who can see what here.
//
// the cast:
//
//   roles     admin and friend, which the category grants; outsider, which it does
//             not.  each named "{tag}-admin" and so on, since restrictions are
//             set by name.
//   users     Admin (admin, and the seeded admin role), Friend (friend), Both
//             (admin and friend), Outsider (outsider).  Admin and Friend can also
//             sign in to the test api host, through AdminExternalId and
//             FriendExternalId.
//   media     Open - unrestricted, and the category's teaser
//             Spare - unrestricted, nothing else; the one to restrict
//             Covered - unrestricted, and the cover of Place
//             AdminOnly - restricted to admin; filed at Boston, with a face that
//                         is Person's preferred face
//             OutsiderOnly - a video restricted to outsider, so visible to nobody
//   person    Person, with one face on Open (so Friend can see them) and their
//             preferred face on AdminOnly (which Friend cannot)
//   place     Place, a private country with no locations - so it is listed to
//             nobody - whose cover is Covered
internal static class RestrictionUniverse
{
    public static async Task<Universe> Create(TestFixture fixture)
    {
        // the random tail of a v7 guid, for the reason PlaceAdminTests gives
        var tag = Guid.CreateVersion7().ToString("N")[^8..];

        Subject NewSubject(string name) =>
            new(Guid.CreateVersion7(), $"/assets/2001/restrict-{tag}/qvg-fill/{name}.avif", Guid.CreateVersion7());

        var u = new Universe(
            Tag: tag,
            Category: Guid.CreateVersion7(),
            Person: Guid.CreateVersion7(),
            OpenFace: Guid.CreateVersion7(),
            Place: Guid.CreateVersion7(),
            AdminRole: $"{tag}-admin",
            FriendRole: $"{tag}-friend",
            OutsiderRole: $"{tag}-outsider",
            Admin: Guid.CreateVersion7(),
            Friend: Guid.CreateVersion7(),
            Both: Guid.CreateVersion7(),
            Outsider: Guid.CreateVersion7(),
            AdminExternalId: $"restrict-admin-{tag}",
            FriendExternalId: $"restrict-friend-{tag}",
            Open: NewSubject("open"),
            Spare: NewSubject("spare"),
            Covered: NewSubject("covered"),
            AdminOnly: NewSubject("admin"),
            OutsiderOnly: NewSubject("outsider"));

        await using var conn = fixture.SetupDataSource.CreateConnection();

        await conn.ExecuteAsync(
            """
            INSERT INTO media.user (id, created, modified, name, email) VALUES
                (@admin,    NOW(), NOW(), @tag || ' admin',    @tag || '-admin@example.test'),
                (@friend,   NOW(), NOW(), @tag || ' friend',   @tag || '-friend@example.test'),
                (@both,     NOW(), NOW(), @tag || ' both',     @tag || '-both@example.test'),
                (@outsider, NOW(), NOW(), @tag || ' outsider', @tag || '-outsider@example.test');

            INSERT INTO media.external_identity (external_id, user_id, created, modified, name, email, email_verified) VALUES
                (@adminExternalId,  @admin,  NOW(), NOW(), @tag || ' admin',  @tag || '-admin@example.test',  true),
                (@friendExternalId, @friend, NOW(), NOW(), @tag || ' friend', @tag || '-friend@example.test', true);

            INSERT INTO media.role (id, name, created, created_by) VALUES
                (@roleAdmin,    @adminRole,    NOW(), @createdBy),
                (@roleFriend,   @friendRole,   NOW(), @createdBy),
                (@roleOutsider, @outsiderRole, NOW(), @createdBy);

            INSERT INTO media.user_role (user_id, role_id, created, created_by) VALUES
                (@admin,    @roleAdmin,    NOW(), @createdBy),
                (@admin,    @seededAdmin,  NOW(), @createdBy),
                (@friend,   @roleFriend,   NOW(), @createdBy),
                (@both,     @roleAdmin,    NOW(), @createdBy),
                (@both,     @roleFriend,   NOW(), @createdBy),
                (@outsider, @roleOutsider, NOW(), @createdBy);

            -- owned by the private admin, so the teaser rule - owner only - applies
            -- to them
            INSERT INTO media.category (id, name, slug, effective_date, created, created_by, modified, modified_by)
            VALUES (@category, @tag || ' restricted', 'restrict-' || @tag, '2001-01-01', NOW(), @admin, NOW(), @admin);

            -- granted to admin and friend; outsider is deliberately left off
            INSERT INTO media.category_role (category_id, role_id, created, created_by) VALUES
                (@category, @roleAdmin,  NOW(), @createdBy),
                (@category, @roleFriend, NOW(), @createdBy);

            INSERT INTO media.media (id, type_id, location_id, created, created_by, modified, modified_by) VALUES
                (@open,         @photo, NULL,        NOW(), @createdBy, NOW(), @createdBy),
                (@spare,        @photo, NULL,        NOW(), @createdBy, NOW(), @createdBy),
                (@covered,      @photo, NULL,        NOW(), @createdBy, NOW(), @createdBy),
                (@adminOnly,    @photo, @locationMa, NOW(), @createdBy, NOW(), @createdBy),
                (@outsiderOnly, @video, NULL,        NOW(), @createdBy, NOW(), @createdBy);

            INSERT INTO media.category_media (category_id, media_id, slug, is_teaser, created, created_by, modified, modified_by) VALUES
                (@category, @open,         'open',     true,  NOW(), @createdBy, NOW(), @createdBy),
                (@category, @spare,        'spare',    false, NOW(), @createdBy, NOW(), @createdBy),
                (@category, @covered,      'covered',  false, NOW(), @createdBy, NOW(), @createdBy),
                (@category, @adminOnly,    'admin',    false, NOW(), @createdBy, NOW(), @createdBy),
                (@category, @outsiderOnly, 'outsider', false, NOW(), @createdBy, NOW(), @createdBy);

            INSERT INTO media.file (id, media_id, type_id, scale_id, width, height, bytes, path) VALUES
                (gen_random_uuid(), @open,         @photo, @scale, 320, 240, 1, @openPath),
                (gen_random_uuid(), @spare,        @photo, @scale, 320, 240, 1, @sparePath),
                (gen_random_uuid(), @covered,      @photo, @scale, 320, 240, 1, @coveredPath),
                (gen_random_uuid(), @adminOnly,    @photo, @scale, 320, 240, 1, @adminPath),
                (gen_random_uuid(), @outsiderOnly, @video, @scale, 320, 240, 1, @outsiderPath);

            INSERT INTO media.person (id, name, slug, status_code, preferred_face_id, face_count, source_revision, published)
            VALUES (@person, @tag || ' person', 'restrict-person-' || @tag, NULL, @adminFace, 2, 1, NOW());

            INSERT INTO media.face (id, media_id, person_id, box_x, box_y, box_width, box_height, detection_score, source_revision, published) VALUES
                (@adminFace, @adminOnly, @person, 0.1, 0.1, 0.2, 0.2, 0.99, 1, NOW()),
                (@openFace,  @open,      @person, 0.1, 0.1, 0.2, 0.2, 0.99, 1, NOW());

            -- a country with no locations, so get_places lists it to nobody, and a
            -- cover - set directly, since publishing one needs files on disk
            INSERT INTO media.place (id, parent_id, kind, name, slug, created, modified, cover_media_id, cover_file_id, cover_created, cover_created_by)
            VALUES (@place, NULL, 'country', @tag || ' place', 'restrict-place-' || @tag, NOW(), NOW(),
                    @covered, (SELECT id FROM media.file WHERE media_id = @covered), NOW(), @createdBy);

            INSERT INTO media.media_role (media_id, role_id, created, created_by) VALUES
                (@adminOnly,    @roleAdmin,    NOW(), @createdBy),
                (@outsiderOnly, @roleOutsider, NOW(), @createdBy);
            """,
            new
            {
                tag,
                createdBy = Constants.USER_ADMIN,
                admin = u.Admin,
                friend = u.Friend,
                both = u.Both,
                outsider = u.Outsider,
                adminExternalId = u.AdminExternalId,
                friendExternalId = u.FriendExternalId,
                roleAdmin = Guid.CreateVersion7(),
                roleFriend = Guid.CreateVersion7(),
                roleOutsider = Guid.CreateVersion7(),
                adminRole = u.AdminRole,
                friendRole = u.FriendRole,
                outsiderRole = u.OutsiderRole,
                seededAdmin = Constants.ROLE_ADMIN,
                category = u.Category,
                photo = Constants.TYPE_PHOTO,
                video = Constants.TYPE_VIDEO,
                scale = Constants.SCALE_QVG_FILL,
                locationMa = Constants.LOCATION_MA.Id,
                open = u.Open.Id,
                spare = u.Spare.Id,
                covered = u.Covered.Id,
                adminOnly = u.AdminOnly.Id,
                outsiderOnly = u.OutsiderOnly.Id,
                openPath = u.Open.Path,
                sparePath = u.Spare.Path,
                coveredPath = u.Covered.Path,
                adminPath = u.AdminOnly.Path,
                outsiderPath = u.OutsiderOnly.Path,
                adminFace = u.AdminOnly.FaceId,
                openFace = u.OpenFace,
                person = u.Person,
                place = u.Place
            });

        return u;
    }
}

internal record Subject(Guid Id, string Path, Guid FaceId);

internal record Universe(
    string Tag,
    Guid Category,
    Guid Person,
    Guid OpenFace,
    Guid Place,
    string AdminRole,
    string FriendRole,
    string OutsiderRole,
    Guid Admin,
    Guid Friend,
    Guid Both,
    Guid Outsider,
    string AdminExternalId,
    string FriendExternalId,
    Subject Open,
    Subject Spare,
    Subject Covered,
    Subject AdminOnly,
    Subject OutsiderOnly);
