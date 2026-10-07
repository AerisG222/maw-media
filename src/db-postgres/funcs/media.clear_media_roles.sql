-- removes the restriction from one or more media, so each follows its category
-- again.
--
--   0  done
--   1  the caller is not an admin
--
-- nothing can block this.  every guard media.set_media_roles applies exists to
-- stop a restricted media leaking through a wider audience, and lifting a
-- restriction only ever widens who may see a media to what its category already
-- allows.  ids naming no media, or media with no restriction, are skipped rather
-- than refused, matching media.bulk_clear_media_gps_override.
CREATE OR REPLACE FUNCTION media.clear_media_roles
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

    DELETE FROM media.media_role mr
    WHERE mr.media_id = ANY(_media_ids);

    RETURN 0;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.clear_media_roles
    TO maw_media;
