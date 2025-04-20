CREATE TABLE IF NOT EXISTS media.media_file (
    id UUID NOT NULL,
    media_id UUID NOT NULL,
    media_type_id UUID NOT NULL,
    scale_id UUID NOT NULL,
    width INTEGER,
    height INTEGER,
    bytes INTEGER,
    path TEXT,

    CONSTRAINT pk_media_media_file
    PRIMARY KEY (id),

    CONSTRAINT fk_media_media_file$media_media
    FOREIGN KEY (media_id)
    REFERENCES media.media(id),

    CONSTRAINT fk_media_media_file$media_scale
    FOREIGN KEY (scale_id)
    REFERENCES media.scale(id),

    CONSTRAINT fk_media_media_file$media_media_type
    FOREIGN KEY (media_type_id)
    REFERENCES media.media_type(id)
);

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'media_file'
            AND indexname = 'ix_media_file$media_id$media_type_id$scale_id'
    )
    THEN

        CREATE INDEX ix_media_file$media_id$media_type_id$scale_id
        ON media.media_file(media_id, media_type_id, scale_id);

    END IF;
END
$$;

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.media_file
TO maw_api;
