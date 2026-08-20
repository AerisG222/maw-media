-- which faces a user may see.
--
-- access is inherited: a face is visible when the media carrying it is, and
-- media visibility already lives in media.user_media (category_role -> user_role
-- -> category_media).  this is the one definition of that rule for face data -
-- every person and face read path goes through it rather than repeating the
-- join and hoping each copy got it right.
--
-- category_id is deliberately absent.  media.user_media has a row per
-- (category, media), so a media item in two categories a user can see appears
-- twice; carrying that through would double count every face in an aggregate.
-- callers needing the category (e.g. "which categories contain this person")
-- join media.user_media separately, which keeps that intent explicit.
CREATE OR REPLACE VIEW media.user_face AS
    SELECT DISTINCT
        um.user_id,
        f.id AS face_id,
        f.person_id,
        f.media_id
    FROM media.user_media um
    INNER JOIN media.face f
        ON f.media_id = um.media_id;

GRANT SELECT
ON media.user_face
TO maw_media;
