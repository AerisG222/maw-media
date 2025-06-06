CREATE TABLE IF NOT EXISTS media.favorite (
    media_id UUID NOT NULL,
    created_by UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_media_favorite
    PRIMARY KEY (media_id, created_by),

    CONSTRAINT fk_media_favorite$media_media
    FOREIGN KEY (media_id)
    REFERENCES media.media(id),

    CONSTRAINT fk_media_favorite$media_user
    FOREIGN KEY (created_by)
    REFERENCES media.user(id)
);

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'favorite'
            AND indexname = 'ix_media_favorite$media_id'
    )
    THEN

        CREATE INDEX ix_media_favorite$media_id
        ON media.favorite(media_id);

    END IF;
END
$$;

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.favorite
TO maw_media;
