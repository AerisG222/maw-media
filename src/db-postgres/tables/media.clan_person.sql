CREATE TABLE IF NOT EXISTS media.clan_person (
    clan_id UUID NOT NULL,
    person_id UUID NOT NULL,

    CONSTRAINT pk_media_clan_person
    PRIMARY KEY (clan_id, person_id),

    CONSTRAINT fk_media_clan_person$media_clan
    FOREIGN KEY (clan_id)
    REFERENCES media.clan(id)
    ON DELETE CASCADE,

    -- CASCADE for the same reason media.person_favorite does: people are
    -- published data, and media.delete_persons hard deletes a cluster that no
    -- longer exists upstream.  a clan membership must not be able to block that;
    -- the clan simply loses a member who no longer exists.
    CONSTRAINT fk_media_clan_person$media_person
    FOREIGN KEY (person_id)
    REFERENCES media.person(id)
    ON DELETE CASCADE
);

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'clan_person'
            AND indexname = 'ix_media_clan_person$person_id'
    )
    THEN
        -- the primary key already covers clan_id; this covers the delete cascade
        -- and any future "which clans is this person in"
        CREATE INDEX ix_media_clan_person$person_id
        ON media.clan_person(person_id);
    END IF;
END
$$;

GRANT SELECT, INSERT, DELETE
ON media.clan_person
TO maw_media;
