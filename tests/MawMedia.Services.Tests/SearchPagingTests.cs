using Dapper;

namespace MawMedia.Services.Tests;

// search reads a materialized view, and a refresh another test started before these rows
// were committed can finish after this one and rebuild the view without them.  so this
// runs on its own, once the tests that refresh in parallel have finished.
[CollectionDefinition(nameof(SearchPagingCollection), DisableParallelization = true)]
public class SearchPagingCollection;

[Collection(nameof(SearchPagingCollection))]
public class SearchPagingTests
{
    readonly TestFixture _fixture;

    public SearchPagingTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    // the caller fetches one more than it keeps and takes the rest from the front, so
    // tied ranks returned in anything but the order they were paged by can keep that
    // extra row and drop a real one: the next page repeats it, and the dropped category
    // is never shown.  named alike so that every one of them ties.  only this user can
    // see them, so no other test's answer moves.
    [Fact]
    public async Task SearchReturnsTiedRanksInTheOrderTheyWerePaged()
    {
        const int categoryCount = 200;
        const int pageSize = 50;

        var token = TestContext.Current.CancellationToken;
        var tag = "tiedrank" + Guid.CreateVersion7().ToString("N")[^8..];
        var term = new string(tag.Where(char.IsLetter).ToArray());
        var user = Guid.CreateVersion7();

        await using (var conn = await _fixture.SetupDataSource.OpenConnectionAsync(token))
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO media.user (id, created, modified, name, email)
                VALUES (@user, NOW(), NOW(), @tag, @tag || '@example.test');

                INSERT INTO media.role (id, name, created, created_by)
                VALUES (@role, @tag, NOW(), @createdBy);

                INSERT INTO media.user_role (user_id, role_id, created, created_by)
                VALUES (@user, @role, NOW(), @createdBy);

                WITH arranged AS
                (
                    SELECT n, gen_random_uuid() AS category_id, gen_random_uuid() AS media_id
                    FROM generate_series(1, @categoryCount) n
                ),
                categories AS
                (
                    INSERT INTO media.category (id, name, slug, effective_date, created, created_by, modified, modified_by)
                    SELECT category_id, @term, @tag || '-' || n, '2001-01-01', NOW(), @createdBy, NOW(), @createdBy
                    FROM arranged
                ),
                category_roles AS
                (
                    INSERT INTO media.category_role (category_id, role_id, created, created_by)
                    SELECT category_id, @role, NOW(), @createdBy
                    FROM arranged
                ),
                media_rows AS
                (
                    INSERT INTO media.media (id, type_id, location_id, created, created_by, modified, modified_by)
                    SELECT media_id, @typePhoto, @location, NOW(), @createdBy, NOW(), @createdBy
                    FROM arranged
                ),
                category_media_rows AS
                (
                    INSERT INTO media.category_media (category_id, media_id, slug, is_teaser, created, created_by, modified, modified_by)
                    SELECT category_id, media_id, 'photo', true, NOW(), @createdBy, NOW(), @createdBy
                    FROM arranged
                )
                INSERT INTO media.file (id, media_id, type_id, scale_id, width, height, bytes, path)
                SELECT gen_random_uuid(), media_id, @typePhoto, @scale, 320, 240, 1, '/assets/' || @tag || '/' || n || '.avif'
                FROM arranged;

                REFRESH MATERIALIZED VIEW media.category_search;
                """,
                new
                {
                    tag,
                    term,
                    user,
                    categoryCount,
                    role = Guid.CreateVersion7(),
                    createdBy = Constants.USER_ADMIN,
                    location = Constants.LOCATION_MA.Id,
                    typePhoto = Constants.TYPE_PHOTO,
                    scale = Constants.SCALE_QVG_FILL
                });
        }

        // which plan postgres picks depends on everything else in the database, and only
        // some of them scatter ties - so the plan that does is forced, or this would pass
        // or fail on what the other tests happened to leave behind.
        await using var read = await _fixture.SetupDataSource.OpenConnectionAsync(token);

        await read.ExecuteAsync("SET enable_nestloop = off; SET enable_mergejoin = off;");

        var rows = await read.QueryAsync<Guid>(
            "SELECT id FROM media.search_categories(@user, @term, 0, @limit, true);",
            new { user, term, limit = pageSize + 1 });

        var returned = rows.Distinct().ToList();

        await read.ExecuteAsync("RESET enable_nestloop; RESET enable_mergejoin;");

        Assert.Equal(pageSize + 1, returned.Count);
        Assert.Equal(returned.Order(), returned);
    }

}
