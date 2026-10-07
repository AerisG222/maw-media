-- the names of every role, for an admin choosing who may see a restricted media.
--
-- null for a caller who is not an admin, and an array - possibly empty - for one
-- who is.  a single value rather than a table so the two answers cannot be
-- confused: "no roles" and "you may not ask" are different things to a client,
-- and an empty result set would be both.
--
-- names rather than ids because that is how media.set_media_roles takes them:
-- a request reads {"roles": ["admin"]}, which is legible in a log and needs no
-- lookup to understand.
CREATE OR REPLACE FUNCTION media.get_roles
(
    _user_id UUID
)
RETURNS TEXT[]
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RETURN NULL;
    END IF;

    RETURN COALESCE(
        (SELECT ARRAY_AGG(r.name ORDER BY r.name) FROM media.role r),
        ARRAY[]::TEXT[]
    );
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_roles
    TO maw_media;
