-- a person (face cluster) published from maw-media-ai.
--
-- this is a projection, not a system of record: maw-media-ai owns the identity
-- of a person and every column below.  ids are the same uuids used there, so a
-- publish is an idempotent upsert on the primary key.  embeddings deliberately
-- never cross over - that is what keeps stock postgres (no pgvector) sufficient
-- here.
--
-- there are no created / modified audit columns, unlike most tables in this
-- schema.  nothing here is user authored: every row is written by the same
-- publisher service account, so created_by / modified_by would record a
-- constant.  `published` covers when the row was last accepted and
-- `source_modified` covers when it last changed upstream, which is the pair
-- that is actually useful when reconciling the two systems.
CREATE TABLE IF NOT EXISTS media.person (
    id UUID NOT NULL,                    -- same uuid as maw-media-ai person.id
    name TEXT,                           -- null until an operator labels the cluster
    slug TEXT,                           -- null until named; drives /person/{slug}
    status_code TEXT,                    -- unknown / not_a_person; null once named
    preferred_face_id UUID,              -- display hint only; no fk, see tables/media.face.sql
    face_count INTEGER NOT NULL DEFAULT 0,
    source_revision BIGINT NOT NULL,     -- monotonic revision from maw-media-ai
    source_modified TIMESTAMPTZ,         -- maw-media-ai clock; informational only, never used to drive sync
    published TIMESTAMPTZ NOT NULL,      -- when this row was last accepted from a publish

    CONSTRAINT pk_media_person
    PRIMARY KEY (id),

    CONSTRAINT uq_media_person$slug
    UNIQUE (slug),

    CONSTRAINT fk_media_person$media_person_status
    FOREIGN KEY (status_code)
    REFERENCES media.person_status(code),

    -- the same invariant maw-media-ai enforces: a named person is not also
    -- triaged.  it can only fire on a sync defect, which is exactly when it
    -- should be loud rather than silently stored.
    CONSTRAINT ck_media_person$name_status
    CHECK (name IS NULL OR status_code IS NULL)
);

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'person'
            AND indexname = 'ix_media_person$name'
    )
    THEN

        -- partial: browsing and search only ever look at people who have a name
        CREATE INDEX ix_media_person$name
        ON media.person(name)
        WHERE name IS NOT NULL;

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
            AND tablename = 'person'
            AND indexname = 'ix_media_person$status_code'
    )
    THEN

        CREATE INDEX ix_media_person$status_code
        ON media.person(status_code)
        WHERE status_code IS NOT NULL;

    END IF;
END
$$;

-- DELETE is required because a sync deletion is a hard delete
GRANT SELECT, INSERT, UPDATE, DELETE
ON media.person
TO maw_media;
