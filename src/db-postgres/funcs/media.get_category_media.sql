CREATE OR REPLACE FUNCTION media.get_category_media
(
    _user_id UUID,
    _category_id UUID
)
RETURNS TABLE
(
    id UUID,
    type TEXT,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT,
    media_is_favorite BOOLEAN
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        md.id,
        md.type,
        md.file_path,
        md.file_type,
        md.file_scale,
        CASE WHEN f.media_id
            IS NOT NULL THEN true
            ELSE false
            END AS media_is_favorite
    FROM media.media m
    INNER JOIN media.user_media um
        ON um.media_id = m.id
    INNER JOIN media.media_detail md
        ON md.id = m.id
    LEFT OUTER JOIN media.favorite f
        ON um.media_id = f.media_id
        AND f.created_by = _user_id
    WHERE
        um.category_id = _category_id
        AND
        um.user_id = _user_id
    ORDER BY m.created;  -- TODO: switch to metadata created
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.get_category_media
   TO maw_media;
