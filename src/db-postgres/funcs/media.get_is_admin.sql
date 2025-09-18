-- todo: quick and dirty permissions to match prior version - permission system to be designed in future
CREATE OR REPLACE FUNCTION media.get_is_admin
(
    _user_id UUID
)
RETURNS BOOLEAN
AS $$
DECLARE
    _is_admin BOOLEAN = FALSE;
BEGIN
    SELECT COUNT(1) > 0 INTO _is_admin
        FROM media.user_role ur
        INNER JOIN media.role r
            ON r.id = ur.role_id
        WHERE
            ur.user_id = _user_id
            AND
            r.name = 'admin';

    RETURN _is_admin;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_is_admin
    TO maw_media;
