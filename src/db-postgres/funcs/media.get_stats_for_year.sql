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
    duration REAL
)
AS $$
BEGIN
    RETURN QUERY
    WITH cat_stats AS
    (
        SELECT
            uc.category_id,
            CASE
                WHEN uc.category_id IS NULL THEN 'Other'
                ELSE cs.category_name
                END AS category_name,
            cs.media_type,
            cs.media_count,
            cs.file_size,
            cs.duration
        FROM media.category_stats cs
        LEFT OUTER JOIN media.user_category uc
            ON uc.category_id = cs.category_id
            AND uc.user_id = _user_id
        WHERE cs.year = _year
    )
    SELECT
        cs.category_id,
        cs.category_name,
        cs.media_type,
        SUM(cs.media_count) AS media_count,
        SUM(cs.file_size) AS file_size,
        SUM(cs.duration) AS duration
    FROM cat_stats cs
    GROUP BY
        cs.category_id,
        cs.category_name,
        cs.media_type
    ORDER BY cs.category_name;


END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_stats_for_year
    TO maw_media;
