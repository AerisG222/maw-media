CREATE TABLE IF NOT EXISTS media.category_role (
    category_id UUID NOT NULL,
    role_id UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    created_by UUID NOT NULL,

    CONSTRAINT pk_media_category_role
    PRIMARY KEY (category_id, role_id),

    CONSTRAINT fk_media_category_role$media_media
    FOREIGN KEY (category_id)
    REFERENCES media.category(id),

    CONSTRAINT fk_media_category_role$media_role
    FOREIGN KEY (role_id)
    REFERENCES media.role(id),

    CONSTRAINT fk_media_category_role$media_user
    FOREIGN KEY (created_by)
    REFERENCES media.user(id)
);

GRANT SELECT, INSERT, DELETE
ON media.category_role
TO maw_media;
