CREATE OR REPLACE FUNCTION media.get_stats
(
    _user_id UUID
)
RETURNS TABLE
(
    year SMALLINT,
    media_type TEXT,
    media_count BIGINT,
    file_size NUMERIC(24),
    duration BIGINT
)
AS $$
BEGIN
    RETURN QUERY
    WITH media_stats AS
    (
        SELECT
            EXTRACT(YEAR FROM c.effective_date) AS category_year,
            m.id AS media_id,
            mt.code AS media_type,
            f.bytes AS file_size,
            CAST(NULL AS INTEGER) AS duration
        FROM media.category c
        INNER JOIN media.category_media cm
            ON cm.category_id = c.id
        INNER JOIN media.media m
            ON cm.media_id = m.id
        INNER JOIN media.type mt
            ON mt.id = m.type_id
        INNER JOIN media.file f
            ON f.media_id = m.id
    )
    SELECT
        CAST(ms.category_year AS SMALLINT) AS category_year,
        ms.media_type,
        COUNT(DISTINCT ms.media_id) AS media_count,
        SUM(ms.file_size) AS file_size,
        SUM(ms.duration) AS duration
    FROM media_stats ms
    GROUP BY
        ms.category_year,
        ms.media_type
    ORDER BY year DESC;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_stats
    TO maw_media;
