-- 2025-11-04 - add slug to return
DROP FUNCTION IF EXISTS media.get_categories;

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
    year SMALLINT,
    slug TEXT,
    effective_date DATE,
    modified TIMESTAMPTZ,
    is_favorite BOOLEAN,
    media_id UUID,
    media_type TEXT,
    media_is_favorite BOOLEAN,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT,
    media_types TEXT[]
)
AS $$
BEGIN
    RETURN QUERY
    -- 2026-09-20 - the categories this call is about, named once.
    --
    -- it exists so media_types below can be computed per category.  as a
    -- correlated subquery in the select list it ran per *output row*, and the
    -- teaser join fans a category out to one row per file - so a listing of 2,163
    -- categories evaluated it 21,968 times and did 1.68M lookups against
    -- media.media to answer 2,163 questions.  that was 4.9s of a 5.0s call.
    --
    -- the filters live here rather than at the bottom so the aggregate is paid
    -- only for categories that survive them: asking for one category by id now
    -- walks one category's media rather than the whole library's.
    WITH visible AS
    (
        SELECT
            c.id,
            c.name,
            c.year,
            c.slug,
            c.effective_date,
            c.modified
        FROM media.category c
        INNER JOIN media.user_category uc
            ON c.id = uc.category_id
            AND uc.user_id = _user_id
        WHERE
            (_id IS NULL OR c.id = _id)
            AND (_year IS NULL OR c.year = _year)
            AND (_modified_after IS NULL OR c.modified::timestamptz(3) > (_modified_after::timestamptz(3) + INTERVAL '1 seconds'))
    ),
    types AS
    (
        -- one row per category, holding the kinds of media it contains.  the same
        -- answer the subquery gave, grouped instead of correlated
        SELECT
            xcm.category_id,
            ARRAY_AGG(DISTINCT xt.code ORDER BY xt.code) AS media_types
        FROM visible v
        -- 2026-10-06 - the caller's media, not the category's, so a media
        -- restricted away from them cannot surface as a type on the tile
        INNER JOIN media.user_media xcm
            ON xcm.category_id = v.id
            AND xcm.user_id = _user_id
        INNER JOIN media.media xm
            ON xm.id = xcm.media_id
        INNER JOIN media.type xt
            ON xt.id = xm.type_id
        GROUP BY xcm.category_id
    )
    SELECT DISTINCT
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
        md.file_path,
        md.file_type,
        md.file_scale,
        t.media_types
    FROM visible c
    -- INNER drops nothing the teaser join below would have kept: a category with
    -- no media has no teaser either, so it was already absent.  an outer join
    -- would only differ for a category this one cannot happen to
    INNER JOIN types t
        ON t.category_id = c.id
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
    ORDER BY c.effective_date DESC;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_categories
    TO maw_media;
