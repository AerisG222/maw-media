-- the roles a category is granted to, for an admin screen to show the current
-- choice.
--
--   result 0  ok - role_names holds the roles, in name order
--   result 1  the caller is not an admin
--   result 2  no such category
--
-- read from media.category_role directly rather than through the caller's own
-- visibility, the way media.get_media_roles is, so an admin can always see what a
-- category is granted.
CREATE OR REPLACE FUNCTION media.get_category_roles
(
    _user_id UUID,
    _category_id UUID,
    OUT result INTEGER,
    OUT role_names TEXT[]
)
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        result := 1;
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM media.category WHERE id = _category_id) THEN
        result := 2;
        RETURN;
    END IF;

    SELECT COALESCE(ARRAY_AGG(r.name ORDER BY r.name), ARRAY[]::TEXT[])
    INTO role_names
    FROM media.category_role cr
    INNER JOIN media.role r
        ON r.id = cr.role_id
    WHERE cr.category_id = _category_id;

    result := 0;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_category_roles
    TO maw_media;
