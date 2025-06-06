CREATE TABLE IF NOT EXISTS media.media_type (
    id UUID NOT NULL,
    name TEXT NOT NULL,

    CONSTRAINT pk_media_type
    PRIMARY KEY (id),

    CONSTRAINT uq_media_type$name
    UNIQUE (name)
);

GRANT SELECT, INSERT, DELETE
ON media.media_type
TO maw_media;
