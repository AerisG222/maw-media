-- 2026-08-22 - add _favorites_only and _seed
-- 2026-08-22 - add _clan_id
DROP FUNCTION IF EXISTS media.get_person_media;

-- the media a user may see that a given person appears in.
--
-- paged, unlike media.get_persons: the person list is a few hundred rows, but a
-- single cluster upstream can hold tens of thousands of faces, so this one has
-- to be requested a page at a time.
--
-- the page is taken over media, not over (media, file) rows.  media_detail
-- returns a row per file, so applying LIMIT to the joined result would slice a
-- media item in half across a page boundary and hand the client a media entry
-- missing some of its scales.  the visible CTE pages first, the join fans out
-- after.
--
-- returns no rows for a person the caller cannot see, or one that is unnamed or
-- triaged - the same rule media.get_persons applies.  the caller cannot use this
-- to confirm a person exists that the person list would not have shown them.
--
-- exactly one of _person_id and _clan_id is expected.  a clan matches media
-- containing *any* of its members, and the DISTINCT ON below means a photo with
-- three of them still counts once - so a page of 25 is 25 photos, not 25 face
-- sightings.  everything else - paging, the favourites filter, the seeded
-- shuffle - behaves identically either way.
CREATE OR REPLACE FUNCTION media.get_person_media
(
    _user_id UUID,
    _person_id UUID,
    _offset INTEGER = 0,
    _limit INTEGER = 25,
    _exclude_src_files BOOLEAN = False,
    _favorites_only BOOLEAN = False,
    _seed BIGINT = NULL,
    _clan_id UUID = NULL
)
RETURNS TABLE
(
    category_id UUID,
    category_year SMALLINT,
    category_slug TEXT,
    media_id UUID,
    media_slug TEXT,
    media_type TEXT,
    media_is_favorite BOOLEAN,
    file_id UUID,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT
)
AS $$
BEGIN
    RETURN QUERY
    WITH visible AS
    (
        -- DISTINCT ON collapses a media item that sits in several categories the
        -- caller can see down to one row.  without it the media would be counted
        -- once per category, so a page of 25 could carry far fewer than 25
        -- distinct photos and its files would be duplicated on the way out.  the
        -- newest category wins, which is the one a client is most likely to link
        -- back to.
        SELECT DISTINCT ON (um.media_id)
            um.media_id,
            um.media_slug,
            um.category_id,
            c.year AS category_year,
            c.slug AS category_slug,
            c.effective_date
        FROM media.user_face uf
        INNER JOIN media.user_media um
            ON um.media_id = uf.media_id
            AND um.user_id = uf.user_id
        INNER JOIN media.category c
            ON c.id = um.category_id
        INNER JOIN media.person p
            ON p.id = uf.person_id
        WHERE
            uf.user_id = _user_id
            AND (
                (_person_id IS NOT NULL AND uf.person_id = _person_id)
                OR
                -- the clan's ownership is checked here rather than by the caller,
                -- so another user's clan id simply matches nothing and reads as
                -- "no such clan" instead of leaking whose it is
                (_clan_id IS NOT NULL AND uf.person_id IN (
                    SELECT cp.person_id
                    FROM media.clan_person cp
                    INNER JOIN media.clan cl
                        ON cl.id = cp.clan_id
                        AND cl.created_by = _user_id
                    WHERE cp.clan_id = _clan_id
                ))
            )
            AND p.name IS NOT NULL
            AND p.status_code IS NULL
            -- EXISTS rather than a join so the filter cannot change the row
            -- count: favorite is keyed on (media_id, created_by), so a join
            -- would be safe today, but the shape of this CTE should not depend
            -- on that.
            AND (
                _favorites_only = FALSE
                OR EXISTS (
                    SELECT 1
                    FROM media.favorite fav
                    WHERE fav.media_id = um.media_id
                        AND fav.created_by = _user_id
                )
            )
        ORDER BY
            um.media_id,
            c.effective_date DESC,
            c.id
    ),
    page AS
    (
        SELECT *
        FROM visible
        ORDER BY
            -- newest first by default.  when _seed is supplied the order is a
            -- shuffle instead - deterministic for a given seed, which is what
            -- makes it safe to page: ORDER BY RANDOM() would reshuffle on every
            -- request, so page 2 would repeat and skip rows from page 1.  the
            -- caller keeps the seed for as long as it wants one ordering, and
            -- picks a new one to reshuffle.
            --
            -- each CASE collapses to NULL for every row when its branch is
            -- inactive, which makes that key a no-op rather than a second
            -- ordering to reason about.
            CASE WHEN _seed IS NULL THEN visible.effective_date END DESC,
            CASE WHEN _seed IS NOT NULL THEN hashtextextended(visible.media_id::TEXT, _seed) END,
            -- media_id breaks ties so paging is stable: several photos share a
            -- category, and therefore an effective_date, and an unordered tie can
            -- repeat or skip rows between one page and the next.
            visible.media_id
        LIMIT _limit
        OFFSET _offset
    )
    SELECT
        pg.category_id,
        pg.category_year,
        pg.category_slug,
        pg.media_id,
        pg.media_slug,
        md.media_type,
        CASE WHEN f.media_id
            IS NOT NULL THEN true
            ELSE false
            END AS media_is_favorite,
        md.file_id,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM page pg
    INNER JOIN media.media_detail md
        ON md.media_id = pg.media_id
        AND (
            _exclude_src_files = FALSE
            OR
            md.file_scale <> 'src'
        )
    LEFT OUTER JOIN media.favorite f
        ON f.media_id = pg.media_id
        AND f.created_by = _user_id
    -- repeated so the file fan-out preserves the page's order
    ORDER BY
        CASE WHEN _seed IS NULL THEN pg.effective_date END DESC,
        CASE WHEN _seed IS NOT NULL THEN hashtextextended(pg.media_id::TEXT, _seed) END,
        pg.media_id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_person_media
    TO maw_media;
