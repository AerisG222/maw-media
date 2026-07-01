CREATE OR REPLACE FUNCTION media.fix_inaccurate_location
(
    _user_id UUID,
    _media_id UUID,
    _new_location_id UUID
)
RETURNS INTEGER
AS $$
DECLARE
    exif_latitude NUMERIC(8,6);
    exif_longitude NUMERIC(9,6);
    mapped_location_id UUID;
    mapped_latitude NUMERIC(8,6);
    mapped_longitude NUMERIC(9,6);
    matching_location_id UUID;
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RAISE NOTICE 'user % is not an admin!', _user_id;
        RETURN 1;
    END IF;

    SELECT
        m.exif_latitude,
        m.exif_longitude,
        m.location_id,
        l.latitude,
        l.longitude
    INTO
        exif_latitude,
        exif_longitude,
        mapped_location_id,
        mapped_latitude,
        mapped_longitude
    FROM media.media_exif_gps m
    LEFT OUTER JOIN media.location l ON l.id = m.location_id
    WHERE m.media_id = _media_id;

    -- if we don't have valid exif data, bail
    IF exif_latitude IS NULL OR exif_longitude IS NULL THEN
        RETURN 10;
    END IF;

    -- if already mapped correctly, no need to update
    IF exif_latitude = mapped_latitude AND exif_longitude = mapped_longitude THEN
        RETURN 11;
    END IF;

    -- use existing location if one exists
    SELECT
        l.id
    INTO
        matching_location_id
    FROM media.location l
    WHERE l.latitude = exif_latitude AND l.longitude = exif_longitude;

    IF matching_location_id IS NOT NULL THEN
        UPDATE media.media
        SET location_id = matching_location_id
        WHERE id = _media_id;

        RETURN 12;
    END IF;

    -- see if there is a location that is very close, that has loaded metadata
    -- if it does, then let's copy the metadata into a new record (primarily
    -- to avoid potential costs for calling the google geocoding API)
    SELECT
        l.id
    INTO
        matching_location_id
    FROM media.location l
    WHERE ABS(exif_latitude - l.latitude) < 0.00001
        AND ABS(exif_longitude - l.longitude) < 0.00001
        AND l.lookup_date IS NOT NULL
    LIMIT 1;

    IF matching_location_id IS NOT NULL THEN
        INSERT INTO media.location (
            id,
            latitude,
            longitude,
            lookup_date,
            formatted_address,
            administrative_area_level_1,
            administrative_area_level_2,
            administrative_area_level_3,
            country,
            locality,
            neighborhood,
            sub_locality_level_1,
            sub_locality_level_2,
            postal_code,
            postal_code_suffix,
            premise,
            route,
            street_number,
            sub_premise
        )
        SELECT
            _new_location_id,
            exif_latitude,
            exif_longitude,
            l.lookup_date,
            l.formatted_address,
            l.administrative_area_level_1,
            l.administrative_area_level_2,
            l.administrative_area_level_3,
            l.country,
            l.locality,
            l.neighborhood,
            l.sub_locality_level_1,
            l.sub_locality_level_2,
            l.postal_code,
            l.postal_code_suffix,
            l.premise,
            l.route,
            l.street_number,
            l.sub_premise
        FROM media.location l
        WHERE l.id = matching_location_id;

        UPDATE media.media
        SET location_id = _new_location_id
        WHERE id = _media_id;

        RETURN 13;
    END IF;

    -- finally, we must create a new location, so do that here
    INSERT INTO media.location (
        id,
        latitude,
        longitude
    )
    VALUES (
        _new_location_id,
        exif_latitude,
        exif_longitude
    );

    UPDATE media.media
    SET location_id = _new_location_id
    WHERE id = _media_id;

    RETURN 14;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.fix_inaccurate_location
    TO maw_media;
