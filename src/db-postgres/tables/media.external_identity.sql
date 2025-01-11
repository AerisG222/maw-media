CREATE TABLE IF NOT EXISTS media.external_identity (
    external_id TEXT NOT NULL,
    user_id UUID,
    created TIMESTAMPTZ NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    email_verified BOOLEAN,
    given_name TEXT,
    surname TEXT,
    picture TEXT,

    CONSTRAINT pk_media_external_identity
    PRIMARY KEY (external_id),

    CONSTRAINT fk_media_external_identity$user
    FOREIGN KEY (user_id)
    REFERENCES media.user(id)
);

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'external_identity'
            AND indexname = 'ix_media_external_identity$user_id'
    )
    THEN

        CREATE INDEX ix_media_external_identity$user_id
        ON media.external_identity(user_id);

    END IF;
END
$$;

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.external_identity
TO maw_api;
