-- records an admin's choice of cover photograph for a place.
--
-- the database half of the operation only.  the caller copies the chosen rendition
-- into the cover directory *before* calling this.  that order is deliberate: a row
-- claiming a cover must never precede the file, because a tile has no way to
-- recover from a broken image.
--
-- nothing has to be cleaned up afterwards.  the file is always {place_id}.avif, so
-- replacing a cover overwrites the one file rather than orphaning a previous
-- name - which is what removes the displaced-file bookkeeping this function used
-- to return.
--
-- admin gated like the other media.set_* functions.  this is the one place in the
-- system where a photograph escapes the per file access check: the copy it names
-- is served to every signed in caller, including those who cannot reach the
-- category the original sits in.  that is the intended behaviour - a place tile
-- has to render for anyone browsing - but it makes the choice a publication of
-- sorts, and only an admin may make it.
--
-- _media_id must sit at _place_id or somewhere beneath it.  a country may
-- therefore be represented by a photograph from one of its cities, which is
-- usually what an admin wants, while an unrelated photograph is refused - the
-- image is meant to represent the place, and the check makes a mis-click
-- impossible rather than merely unlikely.
--
-- return codes:
--   0  set
--   1  caller is not an admin
--   2  no such place
--   3  the media does not sit at this place or beneath it
--
-- see docs/browse-by-location.md

-- 2026-09-01 - the file name is derived from place_id
DROP FUNCTION IF EXISTS media.set_place_cover;

CREATE OR REPLACE FUNCTION media.set_place_cover
(
    _user_id UUID,
    _place_id UUID,
    _media_id UUID,
    _file_id UUID,
    OUT result INTEGER
)
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RAISE NOTICE 'not authorized - user % is not an admin!', _user_id;
        result := 1;
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM media.place WHERE id = _place_id) THEN
        result := 2;
        RETURN;
    END IF;

    -- the admin's own visibility is used rather than a global check, so the rule
    -- is the same one every other read obeys.  in practice an admin holds every
    -- role, but nothing here depends on that being true.
    IF NOT EXISTS (
        SELECT 1
        FROM media.user_location ul
        WHERE ul.user_id = _user_id
            AND ul.media_id = _media_id
            AND ul.place_id IN (
                SELECT d.descendant_id FROM media.get_place_descendants(_place_id) d
            )
    ) THEN
        result := 3;
        RETURN;
    END IF;

    UPDATE media.place
    SET cover_media_id = _media_id,
        cover_file_id = _file_id,
        cover_created = NOW(),
        cover_created_by = _user_id,
        modified = NOW()
    WHERE id = _place_id;

    result := 0;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.set_place_cover
    TO maw_media;
