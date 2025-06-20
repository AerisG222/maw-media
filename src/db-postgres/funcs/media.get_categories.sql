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
    media_id UUID,
    media_type TEXT,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT,
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
        cm.media_id,
        mt.code AS media_type,
        fd.path AS file_path,
        fd.type AS file_type,
        fd.scale AS file_scale,
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
    INNER JOIN media.media m
        ON cm.media_id = m.id
    INNER JOIN media.type mt
        ON m.type_id = mt.id
    INNER JOIN media.file_detail fd
        ON cm.media_id = fd.media_id
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
