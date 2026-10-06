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
    -- the same access rule either way: a face is visible when the media carrying
    -- it is.
    --
    -- 2026-10-06 - joined to media.user_media rather than composing category
    -- access itself.  that one has no DISTINCT any more, so it costs nothing to go
    -- through; and a media may now be restricted to fewer roles than its category
    -- grants, which only media.user_media knows - a face cropped from a restricted
    -- photo must not be fetchable by a caller who cannot open the photo.
    --
    -- still not media.user_face: that one keeps its own DISTINCT, and nothing
    -- here needs one.
    SELECT EXISTS
    (
        SELECT 1
        FROM media.face f
        INNER JOIN media.user_media um
            ON um.media_id = f.media_id
        WHERE
            f.id = _face_id
            AND um.user_id = _user_id
    )
    INTO _can_view;

    RETURN _can_view;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_user_can_view_face
    TO maw_media;
