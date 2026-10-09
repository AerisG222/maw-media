-- 2025-11-04 - add slug to return
DROP FUNCTION IF EXISTS media.get_category_media;

CREATE OR REPLACE FUNCTION media.get_category_media
(
    _user_id UUID,
    _category_id UUID,
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
    -- 2026-09-20 - the category's media, resolved before the file fan-out.
    --
    -- MATERIALIZED is load bearing here rather than a hint.  media.user_media lost
    -- its DISTINCT - it deduplicated nothing, see the view - and without that
    -- barrier the planner flattens the whole thing into one join of eight
    -- relations, which is at join_collapse_limit: it stops searching for an order
    -- and settles for hashing all 1.85M rows of media.file against the 200 this
    -- category actually holds.  measured at 210ms against 4ms.
    --
    -- so the fence is deliberate.  it says: settle which media this is about
    -- first, then go and get their files.  that is also the shape
    -- media.get_place_media and media.get_person_media already use, where a page
    -- of media is chosen before the join that multiplies it per file.
    WITH media_in_category AS MATERIALIZED
    (
        SELECT
            um.category_id,
            c.year AS category_year,
            c.slug AS category_slug,
            um.media_id,
            um.media_slug,
            m.created
        FROM media.media m
        INNER JOIN media.user_media um
            ON um.media_id = m.id
        INNER JOIN media.category c
            ON c.id = um.category_id
        WHERE
            um.category_id = _category_id
            AND
            um.user_id = _user_id
    )
    SELECT
        mic.category_id,
        mic.category_year,
        mic.category_slug,
        md.media_id,
        mic.media_slug,
        md.media_type,
        CASE WHEN f.media_id
            IS NOT NULL THEN true
            ELSE false
            END AS media_is_favorite,
        md.file_id,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM media_in_category mic
    INNER JOIN media.media_detail md
        ON md.media_id = mic.media_id
        AND (
            _exclude_src_files = FALSE
            OR
            md.file_scale <> 'src'
        )
    LEFT OUTER JOIN media.favorite f
        ON f.media_id = mic.media_id
        AND f.created_by = _user_id
    -- capture order.  media.created is not when a media was imported: the
    -- publisher sets it from the file's CreateDate tag, which for photos is the
    -- local camera time (equal to EXIF DateTimeOriginal on 99.98% of them).  so
    -- this already sorts by when each photo was taken, and switching to a
    -- separate metadata date would reorder almost nothing.
    --
    -- the known exception is video: QuickTime stores CreateDate in UTC, so a
    -- video sorts a few hours away from photos taken alongside it.
    ORDER BY mic.created;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.get_category_media
   TO maw_media;
