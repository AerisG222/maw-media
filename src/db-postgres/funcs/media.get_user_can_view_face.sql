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
    -- 2026-09-20 - the rule media.user_face states, composed directly.
    --
    -- reading the view instead meant building it, and it is a DISTINCT over
    -- media.user_media which is itself a DISTINCT: asking about one face built all
    -- 168,818 of the caller's media twice and spilled to disk, for 178ms per call
    -- against 4ms here.  this runs once per face crop, so a screen of them paid it
    -- over and over.
    --
    -- the same reasoning media.user_location documents, and the same access rule
    -- either way: a face is visible when the media carrying it is.
    SELECT EXISTS
    (
        SELECT 1
        FROM media.face f
        INNER JOIN media.category_media cm
            ON cm.media_id = f.media_id
        INNER JOIN media.user_category uc
            ON uc.category_id = cm.category_id
        WHERE
            f.id = _face_id
            AND uc.user_id = _user_id
    )
    INTO _can_view;

    RETURN _can_view;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_user_can_view_face
    TO maw_media;
