CREATE MATERIALIZED VIEW IF NOT EXISTS media.category_stats
AS
WITH
category_stats AS
(
    SELECT
        c.id AS category_id,
        c.name AS category_name,
        c.year,
        m.id AS media_id,
        mt.code AS media_type,
        f.bytes AS file_size,
        CASE WHEN f.scale_id = (SELECT id FROM media.scale WHERE code = 'src')
            THEN m.duration
            ELSE 0
            END AS duration
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
    cs.category_id,
    cs.year,
    cs.category_name,
    cs.media_type,
    CAST(COUNT(DISTINCT cs.media_id) AS INTEGER) AS media_count,
    SUM(cs.file_size) AS file_size,
    SUM(cs.duration) AS duration
FROM category_stats cs
GROUP BY
    cs.category_id,
    cs.year,
    cs.category_name,
    cs.media_type
ORDER BY
    cs.year,
    cs.category_id;


CREATE INDEX IF NOT EXISTS idx_media_category_stats$category_id
ON media.category_stats(category_id);

CREATE INDEX IF NOT EXISTS idx_media_category_stats$year
ON media.category_stats(year);

GRANT SELECT
ON media.category_stats
TO maw_media;
