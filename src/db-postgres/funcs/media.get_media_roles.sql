-- the roles one media is restricted to, for an admin screen to show the current
-- choice.
--
--   result 0  ok - role_names holds the restriction, empty when there is none
--   result 1  the caller is not an admin
--   result 2  no such media
--
-- an empty array means "follows its category", which is the state of every
-- media until somebody restricts one; see tables/media.media_role.sql.
CREATE OR REPLACE FUNCTION media.get_media_roles
(
    _user_id UUID,
    _media_id UUID,
    OUT result INTEGER,
    OUT role_names TEXT[]
)
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        result := 1;
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM media.media WHERE id = _media_id) THEN
        result := 2;
        RETURN;
    END IF;

    SELECT COALESCE(ARRAY_AGG(r.name ORDER BY r.name), ARRAY[]::TEXT[])
    INTO role_names
    FROM media.media_role mr
    INNER JOIN media.role r
        ON r.id = mr.role_id
    WHERE mr.media_id = _media_id;

    result := 0;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_media_roles
    TO maw_media;
