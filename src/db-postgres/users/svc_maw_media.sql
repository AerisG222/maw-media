DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_catalog.pg_user
        WHERE usename = 'svc_maw_media'
    ) THEN

        CREATE USER svc_maw_media;

        GRANT maw_media
        TO svc_maw_media;

        RAISE NOTICE '** created user svc_maw_media.  Please be sure to set the password!';

    END IF;
END
$$;
