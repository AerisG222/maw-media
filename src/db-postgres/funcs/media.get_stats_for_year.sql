CREATE OR REPLACE FUNCTION media.get_stats_for_year
(
    _user_id UUID,
    _year SMALLINT
)
RETURNS TABLE
(
    category_id UUID,
    category_name TEXT,
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
            uc.category_id,
            CASE
                WHEN uc.category_id IS NULL THEN 'Other'
                ELSE c.name
                END AS category_name,
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
        LEFT OUTER JOIN media.user_category uc
            ON uc.category_id = c.id
            AND uc.user_id = _user_id
        WHERE
            EXTRACT(YEAR FROM c.effective_date) = _year
    )
    SELECT
        ms.category_id,
        ms.category_name,
        ms.media_type,
        COUNT(DISTINCT ms.media_id) AS media_count,
        SUM(ms.file_size) AS file_size,
        SUM(ms.duration) AS duration
    FROM media_stats ms
    GROUP BY
        ms.category_id,
        ms.category_name,
        ms.media_type
    ORDER BY ms.category_name;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_stats_for_year
    TO maw_media;
