CREATE TABLE IF NOT EXISTS media.file (
    id UUID NOT NULL,
    media_id UUID NOT NULL,
    type_id UUID NOT NULL,
    scale_id UUID NOT NULL,
    width INTEGER,
    height INTEGER,
    bytes BIGINT,
    path TEXT,

    CONSTRAINT pk_media_file
    PRIMARY KEY (id),

    CONSTRAINT uq_media_file$media_id$type_id$scale_id
    UNIQUE(media_id, type_id, scale_id),

    CONSTRAINT fk_media_file$media_media
    FOREIGN KEY (media_id)
    REFERENCES media.media(id),

    CONSTRAINT fk_media_file$media_scale
    FOREIGN KEY (scale_id)
    REFERENCES media.scale(id),

    CONSTRAINT fk_media_file$media_type
    FOREIGN KEY (type_id)
    REFERENCES media.type(id)
);

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'file'
            AND indexname = 'ix_media_file$media_id'
    )
    THEN

        CREATE INDEX ix_media_file$media_id
        ON media.file(media_id);

    END IF;
END
$$;


GRANT SELECT, INSERT, UPDATE, DELETE
ON media.file
TO maw_media;
