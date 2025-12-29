CREATE OR REPLACE FUNCTION media.get_locations_without_metadata
(
    _user_id UUID
)
RETURNS TABLE
(
    id UUID,
    latitude NUMERIC(8, 6),
    longitude NUMERIC(9, 6)
)
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        -- Not authorized
        RETURN;
    END IF;

    RETURN QUERY
    SELECT
        l.id,
        l.latitude,
        l.longitude
    FROM media.location l
    WHERE l.lookup_date IS NULL;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_locations_without_metadata
    TO maw_media;
