CREATE TABLE IF NOT EXISTS media.category_media (
    category_id UUID NOT NULL,
    media_id UUID NOT NULL,
    is_teaser BOOLEAN NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    created_by UUID NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    modified_by UUID NOT NULL,

    CONSTRAINT pk_media_category_media
    PRIMARY KEY (category_id, media_id),

    CONSTRAINT fk_media_category_media$media_category
    FOREIGN KEY (category_id)
    REFERENCES media.category(id),

    CONSTRAINT fk_media_category_media$media
    FOREIGN KEY (media_id)
    REFERENCES media.media(id),

    CONSTRAINT fk_media_category_media$media_user$created
    FOREIGN KEY (created_by)
    REFERENCES media.user(id),

    CONSTRAINT fk_media_category_media$media_user$modified
    FOREIGN KEY (modified_by)
    REFERENCES media.user(id)
);

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.category_media
TO maw_media;
