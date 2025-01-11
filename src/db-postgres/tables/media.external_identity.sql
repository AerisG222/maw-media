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

GRANT SELECT, INSERT, DELETE
ON media.role
TO maw_api;
