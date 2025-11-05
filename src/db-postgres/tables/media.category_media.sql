CREATE TABLE IF NOT EXISTS media.category_media (
    category_id UUID NOT NULL,
    media_id UUID NOT NULL,
    slug TEXT NOT NULL,
    is_teaser BOOLEAN NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    created_by UUID NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    modified_by UUID NOT NULL,

    CONSTRAINT pk_media_category_media
    PRIMARY KEY (category_id, media_id),

    CONSTRAINT uq_media_category_media$category_id$slug
    UNIQUE(category_id, slug),

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

-- 2025-11-04 - begin - add slug
ALTER TABLE media.category_media
    ADD COLUMN IF NOT EXISTS slug TEXT;

DO
$$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM media.category_media
        WHERE slug IS NULL
    )
    THEN

        WITH
        a AS
        (
            SELECT
                cm.category_id,
                cm.media_id,
                regexp_replace(lower(f.path), '.*\/', '') AS slug
            FROM media.media m
            INNER JOIN media.category_media cm ON cm.media_id = m.id
            INNER JOIN media.file f ON f.media_id = m.id
            INNER JOIN media.scale s ON s.id = f.scale_id AND s.code = 'src'
        ),
        b AS
        (
            SELECT
                category_id,
                media_id,
                regexp_replace(slug, '(.3gp$|.avi$|.flv$|.m4v$|.mkv$|.mov$|.mp4$|.mpeg$|.mpg$|.vob$|.nef$|.dng$|.avif$|.heic$|.jpg$|.png$)', '') AS slug
            FROM a
        ),
        c AS ( SELECT category_id, media_id, replace(slug, ' ', '-')  AS slug FROM b ),
        d AS ( SELECT category_id, media_id, replace(slug, '_', '-')  AS slug FROM c ),
        e AS ( SELECT category_id, media_id, replace(slug, '.', '')   AS slug FROM d ),
        f AS ( SELECT category_id, media_id, replace(slug, '--', '-') AS slug FROM e ),
        g AS ( SELECT category_id, media_id, replace(slug, '--', '-') AS slug FROM f ),
        h AS ( SELECT category_id, media_id, replace(slug, '--', '-') AS slug FROM g ),
        i AS ( SELECT category_id, media_id, replace(slug, '(', '-')  AS slug FROM h ),
        j AS ( SELECT category_id, media_id, replace(slug, ')', '')   AS slug FROM i )
        UPDATE media.category_media cm
            SET slug = j.slug
        FROM j
        WHERE
            cm.category_id = j.category_id
            AND
            cm.media_id = j.media_id;

    END IF;
END
$$;

ALTER TABLE media.category_media
    ALTER COLUMN slug SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE
            conname = 'uq_media_category_media$category_id$slug'
            AND
            conrelid = 'media.category_media'::regclass
    )
    THEN

        ALTER TABLE media.category_media
            ADD CONSTRAINT uq_media_category_media$category_id$slug
            UNIQUE(category_id, slug);

    END IF;
END
$$;
-- 2025-11-04 - end - add slug

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.category_media
TO maw_media;
