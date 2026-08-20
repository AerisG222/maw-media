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
CREATE OR REPLACE FUNCTION media.get_person_media
(
    _user_id UUID,
    _person_id UUID,
    _offset INTEGER = 0,
    _limit INTEGER = 25,
    _exclude_src_files BOOLEAN = False
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
            AND uf.person_id = _person_id
            AND p.name IS NOT NULL
            AND p.status_code IS NULL
        ORDER BY
            um.media_id,
            c.effective_date DESC,
            c.id
    ),
    page AS
    (
        -- media_id breaks ties so paging is stable: several photos share a
        -- category, and therefore an effective_date, and an unordered tie can
        -- repeat or skip rows between one page and the next.
        SELECT *
        FROM visible
        ORDER BY
            visible.effective_date DESC,
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
    ORDER BY
        pg.effective_date DESC,
        pg.media_id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_person_media
    TO maw_media;
