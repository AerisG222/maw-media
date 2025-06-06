CREATE TABLE IF NOT EXISTS media.user (
    id UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    email_verified BOOLEAN,
    given_name TEXT,
    surname TEXT,
    picture_url TEXT,

    CONSTRAINT pk_media_user
    PRIMARY KEY (id)
);

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.user
TO maw_media;
