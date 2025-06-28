CREATE TABLE IF NOT EXISTS media.category (
    id UUID NOT NULL,
    name TEXT NOT NULL,
    effective_date DATE NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    created_by UUID NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    modified_by UUID NOT NULL,

    CONSTRAINT pk_media_category
    PRIMARY KEY (id),

    CONSTRAINT fk_media_category$media_user$created
    FOREIGN KEY (created_by)
    REFERENCES media.user(id),

    CONSTRAINT fk_media_category$media_user$modified
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
            AND tablename = 'category'
            AND indexname = 'ix_media_category_effective_date'
    )
    THEN

        CREATE INDEX ix_media_category_effective_date
        ON media.category(effective_date);

    END IF;
END
$$;

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'category'
            AND indexname = 'ix_media_category_modified'
    )
    THEN

        CREATE INDEX ix_media_category_modified
        ON media.category(modified);

    END IF;
END
$$;

GRANT SELECT, UPDATE
ON media.category
TO maw_media;
