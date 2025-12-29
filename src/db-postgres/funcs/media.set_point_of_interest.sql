CREATE OR REPLACE FUNCTION media.set_point_of_interest
(
    _user_id UUID,
    _location_id UUID,
    _type TEXT,
    _name TEXT
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
        FROM media.point_of_interest
        WHERE
            location_id = _location_id
            AND
            type = _type
    ) THEN
        INSERT INTO media.point_of_interest
        (
            location_id,
            type,
            name
        )
        VALUES
        (
            _location_id,
            _type,
            _name
        );

        RETURN 0;
    END IF;

    UPDATE media.point_of_interest
        SET
            name = _name
        WHERE
            location_id = _location_id
            AND
            type = _type;

    RETURN 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.set_point_of_interest
    TO maw_media;
