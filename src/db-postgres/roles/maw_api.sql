DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_catalog.pg_roles
        WHERE rolname = 'maw_api'
    ) THEN

        CREATE ROLE maw_api;

    END IF;
END
$$;
