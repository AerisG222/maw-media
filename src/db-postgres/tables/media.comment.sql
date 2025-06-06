CREATE TABLE IF NOT EXISTS media.comment (
    id UUID NOT NULL,
    media_id UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    created_by UUID NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    body TEXT,

    CONSTRAINT pk_media_comment
    PRIMARY KEY (id),

    CONSTRAINT fk_media_comment$media_media
    FOREIGN KEY (media_id)
    REFERENCES media.media(id),

    CONSTRAINT fk_media_comment$media_user
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
            AND tablename = 'comment'
            AND indexname = 'ix_media_comment$media_id'
    )
    THEN

        CREATE INDEX ix_media_comment$media_id
        ON media.comment(media_id);

    END IF;
END
$$;

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.comment
TO maw_media;
