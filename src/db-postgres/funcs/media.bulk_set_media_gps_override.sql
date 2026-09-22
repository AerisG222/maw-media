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

    -- the NUMERIC(8, 6) and (9, 6) above do not round anything: postgres drops the
    -- precision of a function parameter, so a coordinate pasted with fourteen
    -- decimals arrives with all fourteen.  the lookup below then compares it with the
    -- six the column stores, never matches, and the insert - which the column does
    -- round - collides with the row that is already there, on the unique
    -- (latitude, longitude) constraint.  the first save of a place worked and every
    -- later one failed.  round here, to what the column holds, so the lookup and the
    -- insert agree about which coordinate this is.
    _latitude := round(_latitude, 6);
    _longitude := round(_longitude, 6);

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
