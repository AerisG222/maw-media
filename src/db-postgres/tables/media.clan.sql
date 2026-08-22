-- a user defined group of people, so a caller can ask for "the kids" rather than
-- re-selecting the same faces every time.
--
-- private to its creator.  clans are a saved query, not shared metadata, and
-- making them visible to others would mean deciding whose access governs the
-- members - the creator's, or the reader's.  keeping them per user leaves that
-- question closed.
CREATE TABLE IF NOT EXISTS media.clan (
    id UUID NOT NULL,
    name TEXT NOT NULL,
    created_by UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    modified TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_media_clan
    PRIMARY KEY (id),

    CONSTRAINT fk_media_clan$media_user
    FOREIGN KEY (created_by)
    REFERENCES media.user(id),

    CONSTRAINT ck_media_clan$name
    CHECK (LENGTH(TRIM(name)) > 0)
);

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'clan'
            AND indexname = 'uq_media_clan$created_by$name'
    )
    THEN
        -- one name per owner: the picker offers these by name, and two clans
        -- called "the kids" would be indistinguishable in it
        CREATE UNIQUE INDEX uq_media_clan$created_by$name
        ON media.clan(created_by, LOWER(TRIM(name)));
    END IF;
END
$$;

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.clan
TO maw_media;
