-- the faces detected in one media item, with their bounding boxes, so a client
-- can draw where the people are on the image.
--
-- access comes through media.user_face, the single definition of "which faces
-- may this caller see" - so a media item the caller cannot reach yields nothing
-- rather than needing a separate check here.
--
-- person_id is null for a face whose person is unnamed or triaged, not just for
-- an unassigned one.  the box is still worth drawing - there *is* a face there -
-- but the person read side hides those clusters, and returning the id would let
-- a caller learn one exists that media.get_persons would never have shown them.
--
-- boxes are normalised 0..1 against the full frame, and may sit slightly outside
-- that range for a face the frame cuts off - clients should clamp when drawing
-- rather than assume.
CREATE OR REPLACE FUNCTION media.get_media_faces
(
    _user_id UUID,
    _media_id UUID
)
RETURNS TABLE
(
    id UUID,
    person_id UUID,
    box_x NUMERIC(7,6),
    box_y NUMERIC(7,6),
    box_width NUMERIC(7,6),
    box_height NUMERIC(7,6)
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        f.id,
        p.id AS person_id,
        f.box_x,
        f.box_y,
        f.box_width,
        f.box_height
    FROM media.user_face uf
    INNER JOIN media.face f
        ON f.id = uf.face_id
    LEFT OUTER JOIN media.person p
        ON p.id = f.person_id
        AND p.name IS NOT NULL
        AND p.status_code IS NULL
    WHERE
        uf.user_id = _user_id
        AND uf.media_id = _media_id
    -- ordered so a client redrawing the same image gets the same overlay order,
    -- which matters for whichever box ends up on top
    ORDER BY
        f.box_x,
        f.box_y,
        f.id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_media_faces
    TO maw_media;
