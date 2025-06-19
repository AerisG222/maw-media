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
    file_type TEXT,
    file_scale TEXT,
    file_path TEXT
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        m.id,
        mt.code AS type,
        f.path AS file_path,
        ft.code AS file_type,
        fs.code AS file_scale
    FROM media.media m
    INNER JOIN media.user_media um
        ON um.category_id = _category_id
        AND um.media_id = m.id
        AND um.user_id = _user_id
    INNER JOIN media.type mt
        ON mt.id = m.type_id
    INNER JOIN media.file f
        ON f.media_id = m.id
    INNER JOIN media.type ft
        ON ft.id = f.type_id
    INNER JOIN media.scale fs
        ON fs.id = f.scale_id
    WHERE
        (_modified_after IS NULL OR m.modified > _modified_after)
    ORDER BY created;  -- switch to metadata created
END

$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.get_category_media
   TO maw_media;
