CREATE TABLE IF NOT EXISTS media.category (
    id UUID NOT NULL,
    name TEXT NOT NULL,
    slug TEXT NOT NULL,
    effective_date DATE NOT NULL,
    year SMALLINT GENERATED ALWAYS AS (CAST(EXTRACT(YEAR FROM effective_date) AS SMALLINT)) STORED,
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

-- 2025-11-04 - begin - add slug
ALTER TABLE media.category
    ADD COLUMN IF NOT EXISTS slug TEXT,
    ADD COLUMN IF NOT EXISTS year SMALLINT GENERATED ALWAYS AS (CAST(EXTRACT(YEAR FROM effective_date) AS SMALLINT)) STORED;

DO
$$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM media.category
        WHERE slug IS NULL
    )
    THEN

        WITH
            a AS ( SELECT id, year, REPLACE(LOWER(name), ' - ','-') AS slug FROM media.category),
            b AS ( SELECT id, year, REPLACE(slug, '''', '')         AS slug FROM a ),
            c AS ( SELECT id, year, REPLACE(slug, ' ', '-')         AS slug FROM b ),
            d AS ( SELECT id, year, REPLACE(slug, '(', '')          AS slug FROM c ),
            e AS ( SELECT id, year, REPLACE(slug, ')', '')          AS slug FROM d ),
            f AS ( SELECT id, year, REPLACE(slug, ')', '')          AS slug FROM e ),
            g AS ( SELECT id, year, REPLACE(slug, '&amp;', '')      AS slug FROM f ),
            h AS ( SELECT id, year, REPLACE(slug, '&', '')          AS slug FROM g ),
            i AS ( SELECT id, year, REPLACE(slug, '--', '-')        AS slug FROM h ),
            j AS ( SELECT id, year, REPLACE(slug, '.', '')          AS slug FROM i ),
            k AS ( SELECT id, year, REPLACE(slug, '!', '')          AS slug FROM j ),
            l AS ( SELECT id, year, REPLACE(slug, ',', '')          AS slug FROM k ),
            m AS ( SELECT id, year, REPLACE(slug, '#', '')          AS slug FROM l ),
            n AS ( SELECT id, year, REPLACE(slug, '?', '')          AS slug FROM m ),
            zz AS (
                SELECT
                    id,
                    year,
                    slug
                FROM n
            )
        UPDATE media.category c
            SET slug = zz.slug
        FROM zz
        WHERE c.id = zz.id;

    END IF;
END
$$;

ALTER TABLE media.category
    ALTER COLUMN slug SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'category'
            AND indexname = 'uq_media_category$year$slug'
    )
    THEN

        -- can not create unique constraint w/ an expression
        CREATE UNIQUE INDEX uq_media_category$year$slug
        ON media.category(
            year,
            slug
        );

    END IF;
END
$$;
-- 2025-11-04 - end - add slug

GRANT SELECT, UPDATE
ON media.category
TO maw_media;
