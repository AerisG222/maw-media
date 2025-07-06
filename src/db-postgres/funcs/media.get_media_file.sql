CREATE OR REPLACE FUNCTION media.get_media_file
(
    _user_id UUID,
    _file_id UUID DEFAULT NULL,
    _path TEXT DEFAULT NULL
)
RETURNS TABLE
(
    file_id UUID,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT
)
AS $$
BEGIN
    IF _file_id IS NULL AND _path IS NULL THEN
        RAISE EXCEPTION 'Either file_id or path must be provided';
    END IF;

    RETURN QUERY
    SELECT
        md.file_id,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM media.user_media um
    INNER JOIN media.media_detail md
        ON md.media_id = um.media_id
    WHERE
        um.user_id = _user_id
        AND
        (_file_id IS NULL OR md.file_id = _file_id)
        AND
        (_path IS NULL OR md.file_path = _path);
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_media_file
    TO maw_media;
