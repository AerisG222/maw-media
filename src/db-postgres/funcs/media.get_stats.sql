CREATE OR REPLACE FUNCTION media.get_stats
(
    _user_id UUID
)
RETURNS TABLE
(
    year SMALLINT,
    category_count BIGINT,
    media_type TEXT,
    media_count BIGINT,
    file_size NUMERIC(24),
    duration INTEGER
)
AS $$
BEGIN
    RETURN QUERY
    WITH media_stats AS
    (
        SELECT
            CAST(EXTRACT(YEAR FROM c.effective_date) AS SMALLINT) AS year,
            mt.code AS media_type,
            COUNT(DISTINCT m.id) AS media_count,
            SUM(f.bytes) AS file_size,
            MAX(m.duration) AS duration
        FROM media.category c
        INNER JOIN media.category_media cm
            ON cm.category_id = c.id
        INNER JOIN media.media m
            ON cm.media_id = m.id
        INNER JOIN media.type mt
            ON mt.id = m.type_id
        INNER JOIN media.file f
            ON f.media_id = m.id
        GROUP BY
            year,
            media_type
    )
    SELECT
        ms.year,
        (SELECT COUNT(cat.id) FROM media.category cat WHERE ms.year = CAST(EXTRACT(YEAR FROM cat.effective_date) AS SMALLINT)) AS category_count,
        ms.media_type,
        ms.media_count,
        ms.file_size,
        ms.duration
    FROM media_stats ms
    GROUP BY
        ms.year,
        ms.media_type,
        ms.media_count,
        ms.file_size,
        ms.duration
    ORDER BY
        ms.year DESC;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_stats
    TO maw_media;
