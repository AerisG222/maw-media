CREATE OR REPLACE FUNCTION media.get_comments
(
    _user_id UUID,
    _media_id UUID
)
RETURNS TABLE
(
    comment_id UUID,
    created TIMESTAMPTZ,
    created_by TEXT,
    modified TIMESTAMPTZ,
    body TEXT
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        c.id AS comment_id,
        c.created,
        u.name AS created_by,
        c.modified,
        c.body
    FROM media.user_media um
    INNER JOIN media.comment c
        ON c.media_id = um.media_id
    INNER JOIN media.user u
        ON c.created_by = u.id
    WHERE
        um.media_id = _media_id
        AND
        um.user_id = _user_id
    ORDER BY c.created DESC;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_comments
    TO maw_media;
