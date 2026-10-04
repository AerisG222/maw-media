-- removes the operator's correction from many media at once, so each falls back
-- to the coordinate its own file recorded.
--
-- the counterpart of media.bulk_set_media_gps_override, and authorized by the same
-- rule: an admin.  that differs from the single media pair, which checks category
-- ownership instead - the asymmetry is inherited from the two set functions rather
-- than introduced here, and mirroring each one keeps "who may undo this" the same
-- question as "who may do this".
--
-- ids naming no media, or media with no override, are skipped rather than
-- refused, matching how the set function treats an id it cannot find.  a bulk
-- correction is a selection from a screen, and failing the whole batch because one
-- item had nothing to undo would make the client retry work that already landed.
--
-- the place tree and the orphaned media.location rows are handled exactly as
-- media.clear_media_gps_override describes: the first needs nothing, and the
-- second is kept on purpose.
CREATE OR REPLACE FUNCTION media.bulk_clear_media_gps_override
(
    _user_id UUID,
    _media_ids UUID[]
)
RETURNS INTEGER
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RETURN 1;
    END IF;

    UPDATE media.media
    SET location_override_id = NULL
    WHERE id = ANY(_media_ids)
        AND location_override_id IS NOT NULL;

    RETURN 0;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.bulk_clear_media_gps_override
    TO maw_media;
