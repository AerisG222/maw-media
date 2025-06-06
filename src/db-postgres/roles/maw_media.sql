DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_catalog.pg_roles
        WHERE rolname = 'maw_media'
    ) THEN

        CREATE ROLE maw_media;

    END IF;
END
$$;
