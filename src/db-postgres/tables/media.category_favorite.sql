CREATE TABLE IF NOT EXISTS media.category_favorite (
    user_id UUID NOT NULL,
    category_id UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_media_category_favorite
    PRIMARY KEY (user_id, category_id),

    CONSTRAINT fk_media_category_favorite$media_user
    FOREIGN KEY (user_id)
    REFERENCES media.user(id),

    CONSTRAINT fk_media_category_favorite$media_category
    FOREIGN KEY (category_id)
    REFERENCES media.category(id)
);

GRANT SELECT
ON media.category_favorite
TO maw_media;
