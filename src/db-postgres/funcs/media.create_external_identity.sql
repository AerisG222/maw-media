CREATE OR REPLACE FUNCTION media.create_external_identity
(
    _external_id TEXT,
    _name TEXT,
    _email TEXT,
    _email_verified BOOLEAN,
    _given_name TEXT,
    _surname TEXT,
    _picture TEXT
)
RETURNS TABLE
(
    external_id TEXT,
    external_profile_last_updated TIMESTAMPTZ,
    user_id UUID,
    user_last_updated TIMESTAMPTZ,
    is_admin BOOLEAN
)
AS $$
DECLARE
    _user_id UUID;
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM media.external_identity ei
        WHERE ei.external_id = _external_id
    ) THEN
        INSERT INTO media.external_identity
        (
            external_id,
            created,
            modified,
            name,
            email,
            email_verified,
            given_name,
            surname,
            picture
        )
        VALUES
        (
            _external_id,
            NOW(),
            NOW(),
            _name,
            _email,
            _email_verified,
            _given_name,
            _surname,
            _picture
        );

        SELECT id INTO _user_id
            FROM media.user u
            WHERE u.email = _email
                AND _email_verified;

        IF _user_id IS NOT NULL
        THEN
            UPDATE media.external_identity ei
                SET user_id = _user_id
                WHERE ei.external_id = _external_id;

            UPDATE media.user u
                SET
                    name = COALESCE(name, _name),
                    given_name = COALESCE(given_name, _given_name),
                    surname = COALESCE(surname, _surname),
                    picture_url = COALESCE(picture_url, _picture),
                    modified = NOW()
                WHERE u.id = _user_id;
        END IF;
    END IF;

    RETURN QUERY
        SELECT *
        FROM media.get_user_state(_external_id);
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.create_external_identity
   TO maw_media;
