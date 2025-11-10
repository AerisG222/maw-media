CREATE OR REPLACE FUNCTION media.get_categories_without_gps
(
    _user_id UUID,
    _year SMALLINT DEFAULT NULL
)
RETURNS TABLE
(
    id UUID
)
AS $$
BEGIN
    RETURN QUERY
    SELECT DISTINCT
        c.id
    FROM media.category c
    INNER JOIN media.user_category uc
        ON c.id = uc.category_id
        AND uc.user_id = _user_id
    INNER JOIN media.category_media cm
        ON c.id = cm.category_id
    INNER JOIN media.media m
        ON m.id = cm.media_id
        AND m.location_id IS NULL
        AND m.location_override_id IS NULL
    WHERE
        _year IS NULL
        OR
        c.year = _year;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.get_categories_without_gps
   TO maw_media;
