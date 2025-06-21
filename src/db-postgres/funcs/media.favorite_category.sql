CREATE OR REPLACE FUNCTION media.favorite_category
(
    _user_id UUID,
    _category_id UUID,
    _is_favorite BOOLEAN
)
RETURNS INTEGER
AS $$
BEGIN

    IF NOT EXISTS (
        SELECT 1
        FROM media.user_category uc
        WHERE
            uc.user_id = _user_id
            AND uc.category_id = _category_id
    ) THEN
        RAISE NOTICE 'not updating category favorite - user % does not have access to category %!', _user_id, _category_id;
        RETURN 1;
    END IF;

    IF _is_favorite THEN
        IF NOT EXISTS (
            SELECT 1
            FROM media.category_favorite cf
            WHERE
                cf.category_id = _category_id
                AND cf.created_by = _user_id
        ) THEN
            INSERT INTO media.category_favorite
            (
                category_id,
                created_by,
                created
            )
            VALUES
            (
                _category_id,
                _user_id,
                NOW()
            );
        END IF;
    ELSE
        DELETE FROM media.category_favorite
        WHERE
            category_id = _category_id
            AND created_by = _user_id;
    END IF;

    RETURN 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.favorite_category
   TO maw_media;
