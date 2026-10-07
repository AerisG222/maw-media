-- 2025-11-04 - add slug to return
DROP FUNCTION IF EXISTS media.search_categories;

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
    year SMALLINT,
    slug TEXT,
    effective_date DATE,
    modified TIMESTAMPTZ,
    is_favorite BOOLEAN,
    media_id UUID,
    media_type TEXT,
    media_is_favorite BOOLEAN,
    file_id UUID,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT,
    media_types TEXT[]
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
    ),
    types AS
    (
        -- 2026-10-06 - the kinds of media on each category of this page, once.
        --
        -- computed over the caller's media rather than the category's, so a media
        -- restricted away from them cannot surface as a type: "video" on a tile
        -- whose only video is hidden says the hidden one exists.  and computed
        -- here, grouped over the page, rather than as a subquery per output row -
        -- that ran once per teaser file, ~10 per category, and each run rebuilt
        -- the caller's access from scratch.
        SELECT
            xum.category_id,
            ARRAY_AGG(DISTINCT xt.code ORDER BY xt.code) AS media_types
        FROM search_results tsr
        INNER JOIN media.user_media xum
            ON xum.category_id = tsr.category_id
            AND xum.user_id = _user_id
        INNER JOIN media.media xm
            ON xm.id = xum.media_id
        INNER JOIN media.type xt
            ON xt.id = xm.type_id
        GROUP BY xum.category_id
    )
    SELECT
        c.id,
        c.name,
        c.year,
        c.slug,
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
        md.file_scale,
        t.media_types
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
    -- LEFT, as the subquery it replaces returned null rather than dropping the
    -- row - though every category here holds at least the media that put it here
    LEFT OUTER JOIN types t
        ON t.category_id = c.id
    ORDER BY sr.search_rank DESC;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
ON FUNCTION media.search_categories
TO maw_media;
