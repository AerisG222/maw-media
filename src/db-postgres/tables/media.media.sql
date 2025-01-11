CREATE TABLE IF NOT EXISTS media.media (
    id UUID NOT NULL,
    category_id UUID NOT NULL,
    media_type_id UUID NOT NULL,
    location_id UUID,
    location_override_id UUID,
    created TIMESTAMPTZ NOT NULL,
    created_by UUID NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    modified_by UUID NOT NULL,
    metadata JSONB,

    CONSTRAINT pk_media_media
    PRIMARY KEY (id),

    CONSTRAINT fk_media_media$media_category
    FOREIGN KEY (category_id)
    REFERENCES media.category(id),

    CONSTRAINT fk_media_media$media_media_type
    FOREIGN KEY (media_type_id)
    REFERENCES media.media_type(id),

    CONSTRAINT fk_media_media$media_location
    FOREIGN KEY (location_id)
    REFERENCES media.location(id),

    CONSTRAINT fk_media_media$media_location$override
    FOREIGN KEY (location_override_id)
    REFERENCES media.location(id),

    CONSTRAINT fk_media$media_user$created
    FOREIGN KEY (created_by)
    REFERENCES media.user(id),

    CONSTRAINT fk_media_media$media_user$modified
    FOREIGN KEY (modified_by)
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
            AND tablename = 'media'
            AND indexname = 'ix_media_media$category_id'
    )
    THEN

        CREATE INDEX ix_media_media$category_id
        ON media.media(category_id);

    END IF;
END
$$;

GRANT SELECT
ON media.media
TO maw_api;
