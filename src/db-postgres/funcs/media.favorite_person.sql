-- marks a person as a favourite for one user, so the picker can float the people
-- they actually look for to the top.
--
-- the access check is the same one media.get_persons applies: a caller may only
-- favourite someone they can already see.  without it a caller could probe for
-- people by favouriting ids and reading back which ones stuck.
CREATE OR REPLACE FUNCTION media.favorite_person
(
    _user_id UUID,
    _person_id UUID,
    _is_favorite BOOLEAN
)
RETURNS INTEGER
AS $$
BEGIN

    IF NOT EXISTS (
        SELECT 1
        FROM media.user_face uf
        INNER JOIN media.person p
            ON p.id = uf.person_id
        WHERE
            uf.user_id = _user_id
            AND uf.person_id = _person_id
            AND p.name IS NOT NULL
            AND p.status_code IS NULL
    ) THEN
        RAISE NOTICE 'not updating person favorite - user % does not have access to person %!', _user_id, _person_id;
        RETURN 1;
    END IF;

    IF _is_favorite THEN
        INSERT INTO media.person_favorite
        (
            person_id,
            created_by,
            created
        )
        VALUES
        (
            _person_id,
            _user_id,
            NOW()
        )
        -- favouriting twice is a no-op rather than an error: clients retry, and
        -- the second call means the same thing as the first
        ON CONFLICT (created_by, person_id) DO NOTHING;
    ELSE
        DELETE FROM media.person_favorite
        WHERE
            person_id = _person_id
            AND created_by = _user_id;
    END IF;

    RETURN 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.favorite_person
   TO maw_media;
