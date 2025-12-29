CREATE OR REPLACE FUNCTION media.set_location_metadata
(
    _user_id UUID,
    _location_id UUID,
    _lookup_date TIMESTAMPTZ,
    _formatted_address TEXT,
    _administrative_area_level_1 TEXT,
    _administrative_area_level_2 TEXT,
    _administrative_area_level_3 TEXT,
    _country TEXT,
    _locality TEXT,
    _neighborhood TEXT,
    _sub_locality_level_1 TEXT,
    _sub_locality_level_2 TEXT,
    _postal_code TEXT,
    _postal_code_suffix TEXT,
    _premise TEXT,
    _route TEXT,
    _street_number TEXT,
    _sub_premise TEXT
)
RETURNS INTEGER
AS $$
BEGIN

    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RAISE NOTICE 'not authorized - user % is not an admin!', _user_id;
        RETURN 1;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM media.location
        WHERE id = _location_id
    ) THEN
        RAISE NOTICE 'not updating location - % does not exist!', _location_id;
        RETURN 2;
    END IF;

    UPDATE media.location
        SET
            lookup_date = _lookup_date,
            formatted_address = _formatted_address,
            administrative_area_level_1 = _administrative_area_level_1,
            administrative_area_level_2 = _administrative_area_level_2,
            administrative_area_level_3 = _administrative_area_level_3,
            country = _country,
            locality = _locality,
            neighborhood = _neighborhood,
            sub_locality_level_1 = _sub_locality_level_1,
            sub_locality_level_2 = _sub_locality_level_2,
            postal_code = _postal_code,
            postal_code_suffix = _postal_code_suffix,
            premise = _premise,
            route = _route,
            street_number = _street_number,
            sub_premise = _sub_premise
        WHERE id = _location_id;

    RETURN 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.set_location_metadata
    TO maw_media;
