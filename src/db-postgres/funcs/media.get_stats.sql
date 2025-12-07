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
    duration REAL

)
AS $$
BEGIN
    RETURN QUERY
    WITH category_counts AS
    (
        SELECT
            cat.year,
            COUNT(cat.id) AS cat_count
        FROM media.category cat
        GROUP BY cat.year
    )
    SELECT
        cs.year,
        cc.cat_count AS category_count,
        cs.media_type,
        SUM(cs.media_count) AS media_count,
        SUM(cs.file_size) AS file_size,
        SUM(cs.duration) AS duration
    FROM media.category_stats cs
    INNER JOIN category_counts cc
        ON cc.year = cs.year
    GROUP BY
        cs.year,
        cc.cat_count,
        cs.media_type
    ORDER BY
        cs.year DESC;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_stats
    TO maw_media;
