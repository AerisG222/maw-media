DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_catalog.pg_user
        WHERE usename = 'svc_maw_api'
    ) THEN

        CREATE USER svc_maw_api;

        GRANT maw_api
        TO svc_maw_api;

        RAISE NOTICE '** created user svc_maw_api.  Please be sure to set the password!';

    END IF;
END
$$;
