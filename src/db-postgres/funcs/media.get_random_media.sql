CREATE OR REPLACE FUNCTION media.get_random_media
(
    _user_id UUID,
    _count SMALLINT DEFAULT 1
)
RETURNS TABLE
(
    id UUID,
    type TEXT,
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
        m.id,
        mt.code AS type,
        f.path AS file_path,
        ft.code AS file_type,
        fs.code AS file_scale
    FROM random r
    INNER JOIN media.media m
        ON r.media_id = m.id
    INNER JOIN media.type mt
        ON mt.id = m.type_id
    INNER JOIN media.file f
        ON f.media_id = m.id
    INNER JOIN media.type ft
        ON ft.id = f.type_id
    INNER JOIN media.scale fs
        ON fs.id = f.scale_id;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_random_media
    TO maw_media;
