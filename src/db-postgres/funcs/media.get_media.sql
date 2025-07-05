CREATE OR REPLACE FUNCTION media.get_media
(
    _user_id UUID,
    _media_id UUID
)
RETURNS TABLE
(
    media_id UUID,
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
        md.media_id,
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
