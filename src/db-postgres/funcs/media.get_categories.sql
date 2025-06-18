CREATE OR REPLACE FUNCTION media.get_categories
(
    _user_id UUID,
    _id UUID DEFAULT NULL,
    _year SMALLINT DEFAULT NULL,
    _modified_after TIMESTAMPTZ DEFAULT NULL
)
RETURNS TABLE
(
    id UUID,
    name TEXT,
    effective_date DATE,
    modified TIMESTAMPTZ,
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
        c.modified,
        qqvg.path AS qqvg_fill_path,
        qvg.path AS qvg_fill_path,
        CASE WHEN cf.category_id
            IS NOT NULL THEN true
            ELSE false
            END AS is_favorite
    FROM media.category c
    INNER JOIN media.user_category uc
        ON c.id = uc.category_id
        AND uc.user_id = _user_id
    INNER JOIN media.category_media cm
        ON c.id = cm.category_id
        AND cm.is_teaser = true
    INNER JOIN media.file_detail qqvg
        ON cm.media_id = qqvg.media_id
        AND qqvg.scale = 'qqvg-fill'
        AND qqvg.type IN ('photo', 'video-poster')
    LEFT OUTER JOIN media.file_detail qvg
        ON cm.media_id = qvg.media_id
        AND qvg.scale = 'qvg-fill'
        AND qvg.type IN ('photo', 'video-poster')
    LEFT OUTER JOIN media.category_favorite cf
        ON c.id = cf.category_id
        AND cf.created_by = _user_id
    WHERE
        (_id IS NULL OR c.id = _id)
        AND (_year IS NULL OR EXTRACT(YEAR FROM c.effective_date) = _year)
        AND (_modified_after IS NULL OR c.modified > _modified_after)
    ORDER BY c.effective_date DESC;
END

$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.get_categories
   TO maw_media;
