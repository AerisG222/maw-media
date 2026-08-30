-- the one definition of "where was this media taken".
--
-- media.media carries two location columns and no rule for choosing between
-- them: location_id is what the file's gps said, location_override_id is what an
-- operator corrected it to.  media.media_gps returns both side by side and leaves
-- the decision to its caller, which is right for a screen that wants to show the
-- correction - and wrong for everything else, which would each end up restating
-- the precedence and eventually disagreeing.
--
-- this is not a tidiness view.  the phase 0 audit found the override is the
-- *majority* path, not an edge case: 60,472 media are reachable only through
-- location_override_id against 30,847 through location_id, and all 1,471 media
-- carrying both have genuinely different values for the coalesce to resolve.  a
-- read path that reached for location_id alone would silently lose two thirds of
-- the library.
--
-- media without any location are excluded rather than returned with a null - 45%
-- of the table has none, and no caller here has a use for a row that says
-- "somewhere".  callers therefore inner join and get the filter for free.
--
-- see docs/browse-by-location.md
CREATE OR REPLACE VIEW media.media_location AS
    SELECT
        m.id AS media_id,
        COALESCE(m.location_override_id, m.location_id) AS location_id
    FROM media.media m
    WHERE COALESCE(m.location_override_id, m.location_id) IS NOT NULL;

GRANT SELECT
ON media.media_location
TO maw_media;
