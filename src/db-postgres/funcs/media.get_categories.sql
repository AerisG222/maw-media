CREATE OR REPLACE FUNCTION media.get_categories
(
    _user_id UUID,
    _id UUID DEFAULT NULL,
    _year SMALLINT DEFAULT NULL,
    _since_id UUID DEFAULT NULL
)
RETURNS TABLE
(
    id UUID,
    name TEXT,
    effective_date DATE,
    qqvg_fill_path TEXT,
    qvg_fill_path TEXT,
    is_favorite BOOLEAN
)
AS $$
BEGIN
    RETURN QUERY
    SELECT DISTINCT
        c.id,
        c.name,
        c.effective_date,
        qqvg.path AS qqvg_fill_path,
        qvg.path AS qvg_fill_path,
        CASE WHEN cf.category_id
            IS NOT NULL THEN true
            ELSE false
            END AS is_favorite
    FROM media.category c
    INNER JOIN media.category_role cr
        ON c.id = cr.category_id
    INNER JOIN media.role r
        ON cr.role_id = r.id
    INNER JOIN media.user_role ur
        ON r.id = ur.role_id
    INNER JOIN media.user u
        ON ur.user_id = u.id
        AND u.id = _user_id
    INNER JOIN media.category_media cm
        ON c.id = cm.category_id
        AND cm.is_teaser = true
    INNER JOIN media.media_file qqvg
        ON cm.media_id = qqvg.media_id
        AND qqvg.scale_id = (
            SELECT s.id
            FROM media.scale s
            WHERE s.code = 'qqvg-fill'
        )
        AND qqvg.media_type_id IN (
            SELECT t.id
            FROM media.media_type t
            WHERE t.name IN ('photo', 'video-poster')
        )
    LEFT OUTER JOIN media.media_file qvg
        ON cm.media_id = qvg.media_id
        AND qvg.scale_id = (
            SELECT s.id
            FROM media.scale s
            WHERE s.code = 'qvg-fill'
        )
        AND qvg.media_type_id IN (
            SELECT t.id
            FROM media.media_type t
            WHERE t.name IN ('photo', 'video-poster')
        )
    LEFT OUTER JOIN media.category_favorite cf
        ON c.id = cf.category_id
        AND cf.created_by = _user_id
    WHERE
        (_id IS NULL OR c.id = _id)
        AND (_year IS NULL OR EXTRACT(YEAR FROM c.effective_date) = _year)
        AND (_since_id IS NULL OR c.id > _since_id)
    ORDER BY c.id;
END

$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.get_categories
   TO maw_media;
