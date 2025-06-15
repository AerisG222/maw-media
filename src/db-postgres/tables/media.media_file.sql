CREATE TABLE IF NOT EXISTS media.media_file (
    media_id UUID NOT NULL,
    type_id UUID NOT NULL,
    scale_id UUID NOT NULL,
    width INTEGER,
    height INTEGER,
    bytes BIGINT,
    path TEXT,

    CONSTRAINT pk_media_media_file
    PRIMARY KEY (media_id, type_id, scale_id),

    CONSTRAINT fk_media_media_file$media_media
    FOREIGN KEY (media_id)
    REFERENCES media.media(id),

    CONSTRAINT fk_media_media_file$media_scale
    FOREIGN KEY (scale_id)
    REFERENCES media.scale(id),

    CONSTRAINT fk_media_media_file$media_type
    FOREIGN KEY (type_id)
    REFERENCES media.type(id)
);

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.media_file
TO maw_media;
