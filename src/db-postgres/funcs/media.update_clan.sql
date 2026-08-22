-- renames a clan.  membership is set separately by media.set_clan_persons, since
-- the two are edited independently in the ui and a rename should not have to
-- resend the member list.
--
-- returns 0 on success, 1 when the clan does not exist or is not the caller's,
-- and 3 when the caller already has another clan by that name - matching the
-- codes media.create_clan returns.
CREATE OR REPLACE FUNCTION media.update_clan
(
    _user_id UUID,
    _clan_id UUID,
    _name TEXT
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
        RAISE NOTICE 'not updating clan - clan % is not owned by user %!', _clan_id, _user_id;
        RETURN 1;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM media.clan c
        WHERE
            c.created_by = _user_id
            AND c.id <> _clan_id
            AND LOWER(TRIM(c.name)) = LOWER(TRIM(_name))
    ) THEN
        RAISE NOTICE 'not updating clan - user % already has one named %!', _user_id, _name;
        RETURN 3;
    END IF;

    UPDATE media.clan
    SET
        name = TRIM(_name),
        modified = NOW()
    WHERE id = _clan_id;

    RETURN 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.update_clan
   TO maw_media;
