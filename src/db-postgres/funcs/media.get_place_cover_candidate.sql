-- 2026-09-01 - select one scale rather than ranking every rendition
DROP FUNCTION IF EXISTS media.get_place_cover_candidate;

-- the rendition of one media that may be published as a place cover.
--
-- exists so the choice of *which* file to copy is made against the database
-- rather than by probing the asset directory, and so two rules govern it:
--
--   exactly one scale is eligible, and 'src' is never returned.
--
-- the caller names the scale - media covers use 'qvg-fill', which is cropped to
-- fill, so every tile comes out the same shape whatever the source photograph's
-- aspect.  ranking renditions by size instead, as this function first did, meant
-- a portrait original produced a portrait cover and the grid looked broken.
--
-- a media without that rendition yields no rows and simply cannot be a cover.
--
-- that is not tidiness, it is the privacy boundary.  a cover is copied to a
-- directory that skips the per file access check, so it reaches every signed in
-- caller, and the original files carry full exif - camera make, serial, and for
-- anything shot on a phone the gps coordinates where the photograph was taken.
-- the derived renditions carry none of that: they hold only structural avif
-- metadata, which is why copying their bytes verbatim is safe and no re-encoding
-- step is needed.
--
-- returns nothing for a media the caller cannot see, so this cannot be used to
-- discover file paths behind a permission the caller lacks.
CREATE OR REPLACE FUNCTION media.get_place_cover_candidate
(
    _user_id UUID,
    _media_id UUID,
    _scale TEXT
)
RETURNS TABLE
(
    file_id UUID,
    file_path TEXT,
    file_scale TEXT,
    file_type TEXT,
    width INTEGER,
    height INTEGER
)
AS $$
    SELECT
        md.file_id,
        md.file_path,
        md.file_scale,
        md.file_type,
        s.width,
        s.height
    FROM media.media_detail md
    INNER JOIN media.scale s
        ON s.code = md.file_scale
    WHERE md.media_id = _media_id
        AND md.file_scale = _scale
        -- belt and braces: even asked for it by name, the original is refused.
        -- it is the one rendition carrying gps and camera identity.
        AND md.file_scale <> 'src'
        AND EXISTS (
            SELECT 1
            FROM media.user_media um
            WHERE um.media_id = _media_id
                AND um.user_id = _user_id
        )
    LIMIT 1;
$$ LANGUAGE sql STABLE;

GRANT EXECUTE
    ON FUNCTION media.get_place_cover_candidate
    TO maw_media;
