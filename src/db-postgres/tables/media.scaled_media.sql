CREATE TABLE IF NOT EXISTS media.scaled_media (
    media_id UUID NOT NULL,
    dimension_id UUID NOT NULL,
    media_type_id UUID NOT NULL,
    height INTEGER,
    width INTEGER,
    bytes INTEGER,
    path TEXT,

    CONSTRAINT pk_media_scaled_media
    PRIMARY KEY (media_id, dimension_id),

    CONSTRAINT fk_media_scaled_media$media_media
    FOREIGN KEY (media_id)
    REFERENCES media.media(id),

    CONSTRAINT fk_media_scaled_media$media_dimension
    FOREIGN KEY (dimension_id)
    REFERENCES media.dimension(id),

    CONSTRAINT fk_media_scaled_media$media_media_type
    FOREIGN KEY (media_type_id)
    REFERENCES media.media_type(id)
);

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.scaled_media
TO maw_api;
