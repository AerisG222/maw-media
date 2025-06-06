CREATE TABLE IF NOT EXISTS media.role (
    id UUID NOT NULL,
    name TEXT NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    created_by UUID NOT NULL,

    CONSTRAINT pk_media_role
    PRIMARY KEY (id),

    CONSTRAINT uq_media_role$name
    UNIQUE (name),

    CONSTRAINT fk_media_role$media_user
    FOREIGN KEY (created_by)
    REFERENCES media.user(id)
);

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.role
TO maw_media;
