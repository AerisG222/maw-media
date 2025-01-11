CREATE TABLE IF NOT EXISTS media.dimension (
    id UUID NOT NULL,
    abbreviation TEXT NOT NULL,
    name TEXT NOT NULL,
    max_width INTEGER NOT NULL,
    max_height INTEGER NOT NULL,
    is_square_cropped BOOLEAN NOT NULL,

    CONSTRAINT pk_media_dimension
    PRIMARY KEY (id),

    CONSTRAINT uq_media_dimension$abbreviation
    UNIQUE (abbreviation),

    CONSTRAINT uq_media_dimension$name
    UNIQUE (name)
);

GRANT SELECT, INSERT, UPDATE
ON media.dimension
TO maw_api;
