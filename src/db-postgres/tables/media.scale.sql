CREATE TABLE IF NOT EXISTS media.scale (
    id UUID NOT NULL,
    code TEXT NOT NULL,
    width INTEGER NOT NULL,
    height INTEGER NOT NULL,
    fills_dimensions BOOLEAN NOT NULL,

    CONSTRAINT pk_media_scale
    PRIMARY KEY (id),

    CONSTRAINT uq_media_scale$code
    UNIQUE (code)
);

GRANT SELECT, INSERT, UPDATE
ON media.scale
TO maw_api;
