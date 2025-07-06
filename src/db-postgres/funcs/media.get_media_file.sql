CREATE OR REPLACE FUNCTION media.get_media_file
(
    _user_id UUID,
    _file_id UUID
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
        md.file_id = _file_id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_media_file
    TO maw_media;
