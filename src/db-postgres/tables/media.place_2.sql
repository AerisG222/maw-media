-- the second half of media.place: its hand picked cover image.
--
-- named for the table rather than for the feature, and numbered, because every
-- other file in this directory is one table and takes that table's name.  there is
-- no media.place_2 table - this is media.place continued, and the number keeps the
-- two adjacent in a listing so neither is read without the other.
--
-- it has to be a second file because the schema's foreign keys form a cycle:
--
--   media.place.cover_media_id  -> media.media
--   media.media.location_id     -> media.location
--   media.location.place_id     -> media.place
--
-- media.place is therefore created before media.media exists, and these columns
-- point at it, so they arrive afterwards as an ALTER.  merging them back into
-- media.place.sql fails the deploy with 'relation "media.media" does not exist'.
--
-- what it adds: an admin chooses one of their own photographs to represent a
-- country, state or city, and a copy of it is published to a directory that any
-- signed in caller may read *without* the per file access check the rest of
-- /assets applies.  a place tile has to render for anyone browsing, including a
-- caller who cannot reach the category the photograph came from.
--
-- cover_media_id keeps the provenance - which photo this came from - so an admin
-- screen can show the current choice and a re-publish can redo it without asking
-- again.  cover_file_id records the exact rendition that was copied, which is what
-- makes it possible to tell later whether a cover predates a change to the
-- rendition pipeline.
--
-- there is no column naming the file.  it is always {place_id}.avif, derivable
-- from the row itself, which is the same choice media.face makes for its crops -
-- no lookup and no directory probe to answer "where is this image".  a cover is
-- therefore replaced by overwriting one file rather than by writing a new one and
-- deleting the old, so nothing has to track a displaced name.
--
-- cover_created doubles as the cache version: the url carries it as ?v=, so a
-- replaced cover is a different url to a browser while remaining one file on
-- disk.  without it the path would be stable and a cached image would outlive the
-- choice that produced it.
--
-- all four are null together or set together.  no constraint enforces it because
-- media.set_place_cover and media.clear_place_cover are the only writers and both
-- move every column at once.
--
-- see docs/browse-by-location.md
ALTER TABLE media.place
    ADD COLUMN IF NOT EXISTS cover_media_id UUID,
    ADD COLUMN IF NOT EXISTS cover_file_id UUID,
    ADD COLUMN IF NOT EXISTS cover_created TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS cover_created_by UUID;

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_media_place$media_media$cover'
            AND conrelid = 'media.place'::regclass
    )
    THEN
        ALTER TABLE media.place
            ADD CONSTRAINT fk_media_place$media_media$cover
            FOREIGN KEY (cover_media_id)
            REFERENCES media.media(id);
    END IF;

    IF NOT EXISTS
    (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_media_place$media_user$cover'
            AND conrelid = 'media.place'::regclass
    )
    THEN
        ALTER TABLE media.place
            ADD CONSTRAINT fk_media_place$media_user$cover
            FOREIGN KEY (cover_created_by)
            REFERENCES media.user(id);
    END IF;
END
$$;

-- 2026-09-01 - the file name is derived from place_id, so the stored one is gone
ALTER TABLE media.place
    DROP COLUMN IF EXISTS cover_path;
