CREATE TABLE IF NOT EXISTS media.role (
    id UUID NOT NULL,
    name TEXT NOT NULL,

    CONSTRAINT pk_media_role
    PRIMARY KEY (id),

    CONSTRAINT uq_media_role$name
    UNIQUE (name)
);

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.role
TO maw_api;
