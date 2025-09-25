CREATE OR REPLACE FUNCTION media.get_user_state
(
    _external_id TEXT
)
RETURNS TABLE
(
    external_id TEXT,
    external_profile_last_updated TIMESTAMPTZ,
    user_id UUID,
    user_last_updated TIMESTAMPTZ,
    is_admin BOOLEAN
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        ei.external_id,
        ei.modified AS external_profile_last_updated,
        ei.user_id,
        u.modified AS user_last_updated,
        (SELECT COUNT(1) > 0
            FROM media.user_role ur
            INNER JOIN media.role r
                ON r.id = ur.role_id
            WHERE
                ur.user_id = ei.user_id
                AND
                r.name = 'admin'
        ) AS is_admin
    FROM media.external_identity ei
    LEFT OUTER JOIN media.user u
        ON u.id = ei.user_id
    WHERE
        ei.external_id = _external_id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_user_state
    TO maw_media;
