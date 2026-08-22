-- replaces a clan's membership wholesale.
--
-- a replace rather than add/remove pairs: the client is a multi select, so it
-- already knows the whole set, and sending it means the call is idempotent and a
-- lost response cannot leave the clan half updated.
--
-- returns 0 on success, 1 when the clan does not exist or is not the caller's,
-- and 2 when any supplied person is not one the caller can see.  an invalid
-- person aborts the whole call rather than being silently dropped: a client
-- sending an id it should not have is a bug, and quietly ignoring it would hide
-- that while appearing to succeed.
CREATE OR REPLACE FUNCTION media.set_clan_persons
(
    _user_id UUID,
    _clan_id UUID,
    _person_ids UUID[]
)
RETURNS INTEGER
AS $$
BEGIN

    IF NOT EXISTS (
        SELECT 1
        FROM media.clan c
        WHERE
            c.id = _clan_id
            AND c.created_by = _user_id
    ) THEN
        RAISE NOTICE 'not updating clan members - clan % is not owned by user %!', _clan_id, _user_id;
        RETURN 1;
    END IF;

    _person_ids := COALESCE(_person_ids, ARRAY[]::UUID[]);

    IF media.get_visible_person_count(_user_id, _person_ids)
        <> (SELECT COUNT(DISTINCT x) FROM UNNEST(_person_ids) AS x)
    THEN
        RAISE NOTICE 'not updating clan members - user % cannot see all of the supplied people!', _user_id;
        RETURN 2;
    END IF;

    DELETE FROM media.clan_person
    WHERE clan_id = _clan_id;

    INSERT INTO media.clan_person
    (
        clan_id,
        person_id
    )
    SELECT DISTINCT
        _clan_id,
        x
    FROM UNNEST(_person_ids) AS x;

    UPDATE media.clan
    SET modified = NOW()
    WHERE id = _clan_id;

    RETURN 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.set_clan_persons
   TO maw_media;
