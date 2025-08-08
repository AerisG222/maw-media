-- we (plan to) only return 24 results from the api at a time - so if we do return a 25th result, then we know more are available
CREATE OR REPLACE FUNCTION media.search_categories
(
    _user_id UUID,
    _search_term TEXT,
    _offset INTEGER = 0,
    _limit INTEGER = 25,
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
    file_id UUID,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT
)
AS $$
BEGIN
    RETURN QUERY
    WITH search_results AS
    (
        SELECT DISTINCT
            uc.category_id,
            TS_RANK(cs.search_vector, PLAINTO_TSQUERY(_search_term)) AS search_rank
        FROM media.user_category uc
        INNER JOIN media.category_search cs
            ON uc.category_id = cs.category_id
            AND cs.search_vector @@ PLAINTO_TSQUERY(_search_term)
        WHERE
            uc.user_id = _user_id
        ORDER BY
            TS_RANK(cs.search_vector, PLAINTO_TSQUERY(_search_term)) DESC,
            uc.category_id
        LIMIT _limit
        OFFSET _offset
    )
    SELECT
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
        md.file_id,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM media.category c
    INNER JOIN search_results sr
        ON c.id = sr.category_id
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
    ORDER BY sr.search_rank DESC;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
ON FUNCTION media.search_categories
TO maw_media;
