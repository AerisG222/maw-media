-- 2025-11-04 - add slug to return
DROP FUNCTION IF EXISTS media.get_random_media;

CREATE OR REPLACE FUNCTION media.get_random_media
(
    _user_id UUID,
    _count SMALLINT = 1,
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

    IF _count < 1 THEN
        _count := 1;
    ELSIF _count > 100 THEN
        _count := 100;
    END IF;

    RETURN QUERY
    WITH random AS
    (
        SELECT
            um.category_id,
            c.year AS category_year,
            c.slug AS category_slug,
            um.media_id,
            um.media_slug
        FROM media.user_media um
        INNER JOIN media.category c
            ON c.id = um.category_id
        WHERE um.user_id = _user_id
        ORDER BY RANDOM()
        LIMIT _count
    )
    SELECT
        r.category_id,
        r.category_year,
        r.category_slug,
        md.media_id,
        r.media_slug,
        md.media_type,
        CASE WHEN f.media_id
            IS NOT NULL THEN true
            ELSE false
            END AS media_is_favorite,
        md.file_id,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM random r
    INNER JOIN media.media_detail md
        ON r.media_id = md.media_id
        AND (
            _exclude_src_files = FALSE
            OR
            md.file_scale <> 'src'
        )
    LEFT OUTER JOIN media.favorite f
        ON r.media_id = f.media_id
        AND f.created_by = _user_id;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_random_media
    TO maw_media;
