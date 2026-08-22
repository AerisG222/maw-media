-- removes a clan.  membership goes with it through the cascade on
-- media.clan_person, and nothing about the people themselves changes - a clan is
-- only ever a saved selection.
--
-- returns 0 on success, 1 when the clan does not exist or is not the caller's.
CREATE OR REPLACE FUNCTION media.delete_clan
(
    _user_id UUID,
    _clan_id UUID
)
RETURNS INTEGER
AS $$
DECLARE
    _deleted INTEGER;
BEGIN

    DELETE FROM media.clan
    WHERE
        id = _clan_id
        AND created_by = _user_id;

    GET DIAGNOSTICS _deleted = ROW_COUNT;

    IF _deleted = 0 THEN
        RAISE NOTICE 'not deleting clan - clan % is not owned by user %!', _clan_id, _user_id;
        RETURN 1;
    END IF;

    RETURN 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.delete_clan
   TO maw_media;
