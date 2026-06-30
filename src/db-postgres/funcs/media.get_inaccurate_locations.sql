CREATE OR REPLACE FUNCTION media.get_inaccurate_locations
(
    _user_id UUID
)
RETURNS TABLE
(
    media_id UUID,
    exif_latitude NUMERIC(8,6),
    exif_longitude NUMERIC(9,6),
    mapped_location_id UUID,
    mapped_latitude NUMERIC(8,6),
    mapped_longitude NUMERIC(9,6)
)
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        -- Not authorized
        RETURN;
    END IF;

    RETURN QUERY
    SELECT
        m.media_id,
        m.exif_latitude,
        m.exif_longitude,
        l.id AS mapped_location_id,
        l.latitude AS mapped_latitude,
        l.longitude AS mapped_longitude
    FROM media.media_exif_gps m
    LEFT OUTER JOIN media.location l ON m.location_id = l.id
    -- exclude media where gps data is missing or invalid
    -- or when it perfectly matches the location, as there is nothing to correct
    WHERE
        m.exif_latitude IS NOT NULL
        AND m.exif_latitude != 0
        AND m.exif_longitude IS NOT NULL
        AND m.exif_longitude != 0
        AND m.exif_latitude != l.latitude
        AND m.exif_longitude != l.longitude
    LIMIT 200;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_inaccurate_locations
    TO maw_media;
