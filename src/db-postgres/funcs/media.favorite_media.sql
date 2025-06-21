CREATE OR REPLACE FUNCTION media.favorite_media
(
    _user_id UUID,
    _media_id UUID,
    _is_favorite BOOLEAN
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
        RAISE NOTICE 'not updating media favorite - user % does not have access to media %!', _user_id, _media_id;
        RETURN 1;
    END IF;

    IF _is_favorite THEN
        IF NOT EXISTS (
            SELECT 1
            FROM media.favorite f
            WHERE
                f.media_id = _media_id
                AND f.created_by = _user_id
        ) THEN
            INSERT INTO media.favorite
            (
                media_id,
                created_by,
                created
            )
            VALUES
            (
                _media_id,
                _user_id,
                NOW()
            );
        END IF;
    ELSE
        DELETE FROM media.favorite
        WHERE
            media_id = _media_id
            AND created_by = _user_id;
    END IF;

    RETURN 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.favorite_media
   TO maw_media;
