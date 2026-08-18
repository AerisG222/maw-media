-- used before storing a published face image, so an upload for an id that was
-- never published answers 404 rather than silently leaving an orphan file in a
-- directory nothing enumerates.
CREATE OR REPLACE FUNCTION media.get_face_exists
(
    _user_id UUID,
    _face_id UUID
)
RETURNS BOOLEAN
AS $$
DECLARE
    _exists BOOLEAN = FALSE;
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RETURN FALSE;
    END IF;

    SELECT COUNT(1) > 0 INTO _exists
        FROM media.face f
        WHERE f.id = _face_id;

    RETURN _exists;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_face_exists
    TO maw_media;
