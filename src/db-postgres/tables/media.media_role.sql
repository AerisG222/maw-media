CREATE TABLE IF NOT EXISTS media.media_role (
    media_id UUID NOT NULL,
    role_id UUID NOT NULL,

    CONSTRAINT pk_media_media_role
    PRIMARY KEY (media_id, role_id),

    CONSTRAINT fk_media_media_role$media_media
    FOREIGN KEY (media_id)
    REFERENCES media.media(id),

    CONSTRAINT fk_media_media_role$media_role
    FOREIGN KEY (role_id)
    REFERENCES media.role(id)
);

GRANT SELECT, INSERT, DELETE
ON media.media_role
TO maw_api;
