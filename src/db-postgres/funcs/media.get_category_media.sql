CREATE OR REPLACE FUNCTION media.get_category_media
(
    _user_id UUID,
    _category_id UUID,
    _modified_after TIMESTAMPTZ DEFAULT NULL
)
RETURNS TABLE
(
    id UUID,
    type TEXT,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        m.id,
        md.type,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM media.media m
    INNER JOIN media.user_media um
        ON um.category_id = _category_id
        AND um.media_id = m.id
        AND um.user_id = _user_id
    INNER JOIN media.media_detail md
        ON m.id = md.id
    WHERE
        (_modified_after IS NULL OR m.modified > _modified_after)
    ORDER BY created;  -- switch to metadata created
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.get_category_media
   TO maw_media;
