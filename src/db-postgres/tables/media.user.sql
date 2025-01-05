CREATE TABLE IF NOT EXISTS media.role (
    id UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    external_id TEXT NOT NULL,
    name TEXT NOT NULL,
    email TEXT,
    picture TEXT,

    CONSTRAINT pk_media_user
    PRIMARY KEY (id)
);

GRANT SELECT, INSERT, DELETE
ON media.role
TO maw_api;
