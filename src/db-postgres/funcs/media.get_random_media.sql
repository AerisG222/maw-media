CREATE OR REPLACE FUNCTION media.get_random_media
(
    _user_id UUID,
    _count SMALLINT DEFAULT 1
)
RETURNS TABLE
(
    media_id UUID,
    media_type TEXT,
    media_is_favorite BOOLEAN,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT
)
AS $$
BEGIN

    IF _count < 1 THEN
        _count := 1;
    ELSIF _count > 100 THEN
        _count := 100;
    END IF;

    RETURN QUERY
    WITH random AS
    (
        SELECT um.media_id
        FROM media.user_media um
        WHERE um.user_id = _user_id
        ORDER BY RANDOM()
        LIMIT _count
    )
    SELECT
        md.media_id,
        md.media_type,
        CASE WHEN f.media_id
            IS NOT NULL THEN true
            ELSE false
            END AS media_is_favorite,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM random r
    INNER JOIN media.media_detail md
        ON r.media_id = md.media_id
    LEFT OUTER JOIN media.favorite f
        ON r.media_id = f.media_id
        AND f.created_by = _user_id;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_random_media
    TO maw_media;
