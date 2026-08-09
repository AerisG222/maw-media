-- triage states for clusters that are deliberately not named.
--
-- deliberately not seeded: maw-media-ai owns these codes, so they arrive with a
-- publish like everything else and this table starts empty.  that keeps one
-- source of truth - a code added there needs no matching deploy here - but it
-- puts an ordering requirement on the sync: media.person.status_code is a
-- foreign key, so statuses must be upserted before the persons that reference
-- them, within the same transaction.
--
-- a lookup table rather than a CHECK so a new state costs an upsert rather than
-- a migration, and the api can expose the valid values.
CREATE TABLE IF NOT EXISTS media.person_status (
    code TEXT NOT NULL,
    label TEXT NOT NULL,
    description TEXT,
    sort_order INTEGER NOT NULL DEFAULT 0,

    CONSTRAINT pk_media_person_status
    PRIMARY KEY (code)
);

-- UPDATE is required because the sync upserts these rows; a label or
-- description edited in maw-media-ai has to be able to land here
GRANT SELECT, INSERT, UPDATE, DELETE
ON media.person_status
TO maw_media;
