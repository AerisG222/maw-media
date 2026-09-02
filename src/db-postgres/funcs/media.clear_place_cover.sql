-- removes a place's cover, returning the file name the caller must delete.
--
-- the mirror of media.set_place_cover, and the order of operations is reversed for
-- the same reason: the row is cleared first, then the file is deleted.  a tile that
-- has already stopped pointing at an image cannot break when the bytes go, whereas
-- deleting first would leave a window in which the row still named a missing file.
--
-- the caller needs nothing back to do that deleting - the file is {place_id}.avif,
-- which it already has.  a place with no cover, or none at all, is not worth
-- distinguishing: both mean there is nothing on disk to remove, and removing
-- nothing is free.
--
-- return codes:
--   0  cleared, or there was nothing to clear
--   1  caller is not an admin
-- 2026-09-01 - the file name is derived from place_id
DROP FUNCTION IF EXISTS media.clear_place_cover;

CREATE OR REPLACE FUNCTION media.clear_place_cover
(
    _user_id UUID,
    _place_id UUID,
    OUT result INTEGER
)
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RAISE NOTICE 'not authorized - user % is not an admin!', _user_id;
        result := 1;
        RETURN;
    END IF;

    UPDATE media.place
    SET cover_media_id = NULL,
        cover_file_id = NULL,
        cover_created = NULL,
        cover_created_by = NULL,
        modified = NOW()
    WHERE id = _place_id;

    result := 0;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.clear_place_cover
    TO maw_media;
