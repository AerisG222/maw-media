CREATE OR REPLACE FUNCTION media.get_categories
(
    _user_id UUID,
    _id UUID DEFAULT NULL,
    _year SMALLINT DEFAULT NULL,
    _modified_after TIMESTAMPTZ DEFAULT NULL,
    _exclude_src_files BOOLEAN = False
)
RETURNS TABLE
(
    id UUID,
    name TEXT,
    effective_date DATE,
    modified TIMESTAMPTZ,
    is_favorite BOOLEAN,
    media_id UUID,
    media_type TEXT,
    media_is_favorite BOOLEAN,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT
)
AS $$
BEGIN
    RETURN QUERY
    SELECT DISTINCT
        c.id,
        c.name,
        c.effective_date,
        c.modified,
        CASE WHEN cf.category_id
            IS NOT NULL THEN true
            ELSE false
            END AS is_favorite,
        cm.media_id,
        md.media_type,
        CASE WHEN f.media_id
            IS NOT NULL THEN true
            ELSE false
            END AS media_is_favorite,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM media.category c
    INNER JOIN media.user_category uc
        ON c.id = uc.category_id
        AND uc.user_id = _user_id
    INNER JOIN media.category_media cm
        ON c.id = cm.category_id
        AND cm.is_teaser = true
    INNER JOIN media.media_detail md
        ON cm.media_id = md.media_id
        AND (
            _exclude_src_files = FALSE
            OR
            md.file_scale <> 'src'
        )
    LEFT OUTER JOIN media.favorite f
        ON cm.media_id = f.media_id
        AND f.created_by = _user_id
    LEFT OUTER JOIN media.category_favorite cf
        ON c.id = cf.category_id
        AND cf.created_by = _user_id
    WHERE
        (_id IS NULL OR c.id = _id)
        AND (_year IS NULL OR EXTRACT(YEAR FROM c.effective_date) = _year)
        AND (_modified_after IS NULL OR c.modified::timestamptz(3) > _modified_after::timestamptz(3))
    ORDER BY c.effective_date DESC;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.get_categories
   TO maw_media;
