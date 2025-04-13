CREATE TABLE IF NOT EXISTS media.favorite_category (
    user_id UUID NOT NULL,
    category_id UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_media_favorite_category
    PRIMARY KEY (user_id, category_id),

    CONSTRAINT fk_media_favorite_category$media_user
    FOREIGN KEY (user_id)
    REFERENCES media.user(id),

    CONSTRAINT fk_media_favorite_category$media_category
    FOREIGN KEY (category_id)
    REFERENCES media.category(id)
);

GRANT SELECT
ON media.favorite_category
TO maw_api;
