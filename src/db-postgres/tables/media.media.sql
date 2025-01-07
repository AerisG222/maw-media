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
    duration_seconds NUMERIC(7.2)

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
    FOREIGN KEY (modifed_by)
    REFERENCES media.user(id),
);

GRANT SELECT
ON media.media
TO maw_api;
