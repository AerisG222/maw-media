-- the media a user may see that were taken at a given place.
--
-- the sibling of media.get_person_media, and deliberately the same shape: same
-- paging contract, same seeded shuffle, same favourites filter, same column list,
-- so a client renders a place's photos with the grid it already has.
--
-- _place_id may be a country, a state or a city.  media.location.place_id records
-- only the deepest place a coordinate resolved to, so the place is expanded
-- through media.get_place_descendants first - without that, a country would match
-- only the handful of coordinates that had no state or city to file them under.
--
-- the page is taken over media, not over (media, file) rows.  media_detail returns
-- a row per file, so applying LIMIT to the joined result would slice a media item
-- in half across a page boundary and hand the client an entry missing some of its
-- scales.  the visible CTE pages first, the join fans out after.
--
-- returns no rows for a place the caller cannot see media at, which is the whole
-- access rule: media.user_location inherits visibility from media.user_category,
-- so a place that exists only in categories the caller has no role for simply
-- returns nothing.  the caller cannot use this to confirm a place exists that
-- media.get_places would not have shown them.
--
-- see docs/browse-by-location.md
CREATE OR REPLACE FUNCTION media.get_place_media
(
    _user_id UUID,
    _place_id UUID,
    _offset INTEGER = 0,
    _limit INTEGER = 25,
    _exclude_src_files BOOLEAN = False,
    _favorites_only BOOLEAN = False,
    _seed BIGINT = NULL
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
    WITH place AS
    (
        -- materialized once rather than correlated into the join below, so the
        -- subtree walk runs a single time per call instead of per candidate row
        SELECT d.descendant_id
        FROM media.get_place_descendants(_place_id) d
    ),
    visible AS
    (
        -- DISTINCT ON collapses a media item that sits in several categories the
        -- caller can see down to one row.  media.user_location is already one row
        -- per (user, place, media), but the join to media.user_media below
        -- reintroduces the category dimension, and without this a photo in three
        -- visible categories would be counted three times - so a page of 50 could
        -- carry far fewer than 50 distinct photos and duplicate their files on the
        -- way out.  the newest category wins, which is the one a client is most
        -- likely to link back to.
        SELECT DISTINCT ON (ul.media_id)
            ul.media_id,
            um.media_slug,
            um.category_id,
            c.year AS category_year,
            c.slug AS category_slug,
            c.effective_date
        FROM media.user_location ul
        INNER JOIN media.user_media um
            ON um.media_id = ul.media_id
            AND um.user_id = ul.user_id
        INNER JOIN media.category c
            ON c.id = um.category_id
        WHERE
            ul.user_id = _user_id
            AND ul.place_id IN (SELECT p.descendant_id FROM place p)
            -- EXISTS rather than a join so the filter cannot change the row count:
            -- favorite is keyed on (media_id, created_by), so a join would be safe
            -- today, but the shape of this CTE should not depend on that.
            AND (
                _favorites_only = FALSE
                OR EXISTS (
                    SELECT 1
                    FROM media.favorite fav
                    WHERE fav.media_id = ul.media_id
                        AND fav.created_by = _user_id
                )
            )
        ORDER BY
            ul.media_id,
            c.effective_date DESC,
            c.id
    ),
    page AS
    (
        SELECT *
        FROM visible
        ORDER BY
            -- newest first by default.  when _seed is supplied the order is a
            -- shuffle instead - deterministic for a given seed, which is what makes
            -- it safe to page: ORDER BY RANDOM() would reshuffle on every request,
            -- so page 2 would repeat and skip rows from page 1.  the caller keeps
            -- the seed for as long as it wants one ordering, and picks a new one to
            -- reshuffle.
            --
            -- each CASE collapses to NULL for every row when its branch is
            -- inactive, which makes that key a no-op rather than a second ordering
            -- to reason about.
            CASE WHEN _seed IS NULL THEN visible.effective_date END DESC,
            CASE WHEN _seed IS NOT NULL THEN hashtextextended(visible.media_id::TEXT, _seed) END,
            -- media_id breaks ties so paging is stable: many photos share a
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
    ON FUNCTION media.get_place_media
    TO maw_media;
