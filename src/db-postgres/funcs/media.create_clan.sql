-- creates a clan and sets its initial membership in one call, so the common path
-- from the picker - select several people, name the group - is a single request.
--
-- returns (clan_id, result): 0 with an id on success, 2 when a person is not one
-- the caller can see, 3 when the caller already has a clan by that name.  the
-- codes line up with media.set_clan_persons so the repository maps one set of
-- outcomes rather than two.
CREATE OR REPLACE FUNCTION media.create_clan
(
    _user_id UUID,
    _name TEXT,
    _person_ids UUID[] = NULL
)
RETURNS TABLE
(
    clan_id UUID,
    result INTEGER
)
AS $$
DECLARE
    _clan_id UUID;
BEGIN

    _person_ids := COALESCE(_person_ids, ARRAY[]::UUID[]);

    -- checked before the insert so a rejected call leaves nothing behind, rather
    -- than relying on the transaction to undo a clan that should never have been
    -- written
    IF media.get_visible_person_count(_user_id, _person_ids)
        <> (SELECT COUNT(DISTINCT x) FROM UNNEST(_person_ids) AS x)
    THEN
        RAISE NOTICE 'not creating clan - user % cannot see all of the supplied people!', _user_id;
        RETURN QUERY SELECT NULL::UUID, 2;
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM media.clan c
        WHERE
            c.created_by = _user_id
            AND LOWER(TRIM(c.name)) = LOWER(TRIM(_name))
    ) THEN
        RAISE NOTICE 'not creating clan - user % already has one named %!', _user_id, _name;
        RETURN QUERY SELECT NULL::UUID, 3;
        RETURN;
    END IF;

    _clan_id := gen_random_uuid();

    INSERT INTO media.clan
    (
        id,
        name,
        created_by,
        created,
        modified
    )
    VALUES
    (
        _clan_id,
        TRIM(_name),
        _user_id,
        NOW(),
        NOW()
    );

    INSERT INTO media.clan_person
    (
        clan_id,
        person_id
    )
    SELECT DISTINCT
        _clan_id,
        x
    FROM UNNEST(_person_ids) AS x;

    RETURN QUERY SELECT _clan_id, 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.create_clan
   TO maw_media;
