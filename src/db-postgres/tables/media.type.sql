CREATE TABLE IF NOT EXISTS media.type (
    id UUID NOT NULL,
    code TEXT NOT NULL,

    CONSTRAINT pk_media_type
    PRIMARY KEY (id),

    CONSTRAINT uq_media_type$code
    UNIQUE (code)
);

GRANT SELECT, INSERT, DELETE
ON media.type
TO maw_media;
