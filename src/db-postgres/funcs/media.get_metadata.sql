CREATE OR REPLACE FUNCTION media.get_metadata
(
    _user_id UUID,
    _media_id UUID
)
RETURNS TABLE
(
    metadata JSONB
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        m.metadata - ARRAY['SourceFile', 'ExifTool', 'File', 'MPF', 'JFIF', 'ICC_Profile', 'XMP'] AS metadata
    FROM media.media m
    INNER JOIN media.user_media um
        ON um.media_id = m.id
    WHERE
        um.media_id = _media_id
        AND
        um.user_id = _user_id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_metadata
    TO maw_media;
