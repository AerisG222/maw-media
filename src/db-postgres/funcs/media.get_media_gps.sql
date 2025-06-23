CREATE OR REPLACE FUNCTION media.get_media_gps
(
    _user_id UUID,
    _media_id UUID DEFAULT NULL,
    _category_id UUID DEFAULT NULL
)
RETURNS TABLE
(
    media_id UUID,
    latitude NUMERIC(8, 6),
    longitude NUMERIC(9, 6)
)
AS $$
BEGIN
    IF _media_id IS NULL AND _category_id IS NULL THEN
        RAISE 'please specify either a media_id or a category_id when calling this function';
    END IF;

    RETURN QUERY
    SELECT
        gps.media_id,
        gps.latitude,
        gps.longitude
    FROM media.user_media um
    INNER JOIN media.media_gps gps
        ON gps.media_id = um.media_id
    WHERE
        um.user_id = _user_id
        AND (_media_id IS NULL OR um.media_id = _media_id)
        AND (_category_id IS NULL OR um.category_id = _category_id)
        AND gps.latitude IS NOT NULL
        AND gps.longitude IS NOT NULL;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_media
    TO maw_media;
