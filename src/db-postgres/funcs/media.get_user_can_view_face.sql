-- whether a user may see one face, used to guard the published face image.
--
-- distinct from media.get_face_exists, which asks "is this face published" for
-- the admin upload path.  this asks "may *this* caller see it", so the image of
-- a person in a category they have no role for cannot be fetched even if the
-- face id is known.
CREATE OR REPLACE FUNCTION media.get_user_can_view_face
(
    _user_id UUID,
    _face_id UUID
)
RETURNS BOOLEAN
AS $$
DECLARE
    _can_view BOOLEAN = FALSE;
BEGIN
    SELECT COUNT(1) > 0 INTO _can_view
        FROM media.user_face uf
        WHERE
            uf.user_id = _user_id
            AND uf.face_id = _face_id;

    RETURN _can_view;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_user_can_view_face
    TO maw_media;
