CREATE TABLE IF NOT EXISTS media.media (
    id UUID NOT NULL,
    type_id UUID NOT NULL,
    location_id UUID,
    location_override_id UUID,
    created TIMESTAMPTZ NOT NULL,
    created_by UUID NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    modified_by UUID NOT NULL,
    duration REAL,
    metadata JSONB,

    CONSTRAINT pk_media_media
    PRIMARY KEY (id),

    CONSTRAINT fk_media_media$media_type
    FOREIGN KEY (type_id)
    REFERENCES media.type(id),

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

GRANT INSERT, UPDATE, SELECT
ON media.media
TO maw_media;
