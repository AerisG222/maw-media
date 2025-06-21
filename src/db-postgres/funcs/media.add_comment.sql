CREATE OR REPLACE FUNCTION media.add_comment
(
    _comment_id UUID,
    _user_id UUID,
    _media_id UUID,
    body TEXT
)
RETURNS INTEGER
AS $$
BEGIN

    IF NOT EXISTS (
        SELECT 1
        FROM media.user_media um
        WHERE
            um.user_id = _user_id
            AND um.media_id = _media_id
    ) THEN
        RAISE NOTICE 'not adding comment - user % does not have access to media %, or media does not exist!', _user_id, _media_id;
        RETURN 1;
    END IF;

    INSERT INTO media.comment
    (
        id,
        media_id,
        created_by,
        created,
        modified,
        body
    )
    VALUES
    (
        _comment_id,
        _media_id,
        _user_id,
        NOW(),
        NOW(),
        body
    );

    RETURN 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.add_comment
    TO maw_media;
