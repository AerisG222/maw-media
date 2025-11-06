-- 2025-11-04 - add slug to return
DROP FUNCTION IF EXISTS media.get_media;

CREATE OR REPLACE FUNCTION media.get_media
(
    _user_id UUID,
    _media_id UUID,
    _exclude_src_files BOOLEAN = False
)
RETURNS TABLE
(
    category_id UUID,
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
    SELECT
        um.category_id,
        md.media_id,
        um.media_slug,
        md.media_type,
        CASE WHEN f.media_id
            IS NOT NULL THEN true
            ELSE false
            END AS media_is_favorite,
        md.file_id,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM media.media m
    INNER JOIN media.user_media um
        ON um.media_id = m.id
    INNER JOIN media.media_detail md
        ON md.media_id = m.id
        AND (
            _exclude_src_files = FALSE
            OR
            md.file_scale <> 'src'
        )
    LEFT OUTER JOIN media.favorite f
        ON um.media_id = f.media_id
        AND f.created_by = _user_id
    WHERE
        um.media_id = _media_id
        AND
        um.user_id = _user_id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_media
    TO maw_media;
