CREATE TABLE IF NOT EXISTS media.user (
    id UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    external_id TEXT NOT NULL,
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    email_verified BOOLEAN,
    given_name TEXT,
    surname TEXT,
    picture TEXT,

    CONSTRAINT pk_media_user
    PRIMARY KEY (id),

    CONSTRAINT uq_media_user$external_id
    UNIQUE (external_id)
);

GRANT SELECT, INSERT, DELETE
ON media.role
TO maw_api;
