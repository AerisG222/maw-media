-- removes the operator's correction from one media, so it falls back to the
-- coordinate its own file recorded.
--
-- the counterpart of media.set_media_gps_override, and authorized by the same
-- rule: whoever may set an override on a media may take it away again.  a caller
-- who owns no category holding the media gets 1, exactly as they would for a set.
--
-- clearing an override that is not there succeeds.  the caller's intent - "this
-- media should have no override" - is already true, and answering 1 would read as
-- a permission failure to a client that cannot tell the two apart.
--
-- what falls out of the place tree needs no help here.  where a media is filed is
-- read through media.media_location as COALESCE(location_override_id,
-- location_id), and place_id lives on media.location, so the media moves back to
-- its recorded coordinate's place on the next read.  one with no recorded
-- coordinate leaves location browsing altogether, which is the honest answer for
-- a photo whose only location was the one being withdrawn.
--
-- the override's media.location row is deliberately left in place, even when this
-- was the last media pointing at it.  locations are one row per coordinate and
-- shared by every media taken there; and a geocoded one carries a lookup that was
-- paid for.  deleting it would mean paying again the next time anyone chooses that
-- coordinate, which is the exact cost the location table exists to avoid.
CREATE OR REPLACE FUNCTION media.clear_media_gps_override
(
    _user_id UUID,
    _media_id UUID
)
RETURNS INTEGER
AS $$
DECLARE
    owner_id UUID;
BEGIN
    -- the same ownership test media.set_media_gps_override applies, and the same
    -- answer for a media that does not exist: no owner is found, so 1
    SELECT c.created_by INTO owner_id
    FROM media.category c
    INNER JOIN media.category_media cm
        ON cm.category_id = c.id
    WHERE cm.media_id = _media_id
    LIMIT 1;

    IF owner_id IS NULL OR owner_id <> _user_id THEN
        RETURN 1;
    END IF;

    -- the IS NOT NULL guard is what keeps an idempotent repeat from writing a row
    -- it would not change
    UPDATE media.media
    SET location_override_id = NULL
    WHERE id = _media_id
        AND location_override_id IS NOT NULL;

    RETURN 0;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.clear_media_gps_override
    TO maw_media;
