CREATE OR REPLACE FUNCTION media.bulk_set_media_gps_override
(
    _user_id UUID,
    _media_ids UUID[],
    _new_location_id UUID,
    _latitude NUMERIC(8, 6),
    _longitude NUMERIC(9, 6)
)
RETURNS INTEGER
AS $$
DECLARE
    override_location_id UUID;
BEGIN
    -- lets just rely on our admin check for now as we have not implemented more granular permissions yet
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        -- Not authorized
        RETURN 1;
    END IF;

    -- Try to find an existing location with the same coordinates
    SELECT id INTO override_location_id
        FROM media.location
        WHERE latitude = _latitude AND longitude = _longitude
        LIMIT 1;

    -- If not found, insert a new location
    IF override_location_id IS NULL THEN
        INSERT INTO media.location
        (
            id,
            latitude,
            longitude
        )
        VALUES
        (
            gen_random_uuid(),
            _latitude,
            _longitude
        )
        RETURNING id INTO override_location_id;
    END IF;

    -- Update the media's location_override_id
    UPDATE media.media
        SET location_override_id = override_location_id
        WHERE id = ANY(_media_ids);

    RETURN 0;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.bulk_set_media_gps_override
    TO maw_media;
