-- 2026-08-27 - initial
DROP FUNCTION IF EXISTS media.get_person_categories;

-- the categories a user may see that a given person (or clan) appears in.
--
-- the sibling of media.get_person_media: same access rule, same person/clan
-- switch, same paging contract - it rolls the person's media up to the
-- categories holding them instead of returning the media itself, so the faces
-- screen can offer "show me the categories" beside "show me the photos".
--
-- exactly one of _person_id and _clan_id is expected.  a clan matches a category
-- holding media of *any* of its members.
--
-- the shape mirrors media.get_categories deliberately - the same columns, the
-- same is_teaser join - so a client renders these tiles with the category grid
-- it already has.  media_count is the extra: how many of the caller's visible
-- media in that category this person appears in.
--
-- note this must NOT collapse a media item down to a single category the way
-- get_person_media's DISTINCT ON does.  there, one photo in three visible
-- categories is still one photo; here it is the reason all three categories
-- belong in the answer.
--
-- returns no rows for a person the caller cannot see, or one that is unnamed or
-- triaged - the same rule media.get_persons applies.
CREATE OR REPLACE FUNCTION media.get_person_categories
(
    _user_id UUID,
    _person_id UUID,
    _offset INTEGER = 0,
    _limit INTEGER = 25,
    _exclude_src_files BOOLEAN = False,
    _favorites_only BOOLEAN = False,
    _clan_id UUID = NULL
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
    media_slug TEXT,
    media_type TEXT,
    media_is_favorite BOOLEAN,
    file_id UUID,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT,
    media_types TEXT[],
    media_count INTEGER
)
AS $$
BEGIN
    RETURN QUERY
    WITH visible AS
    (
        -- one row per category the person appears in, carrying the count of the
        -- caller's media in it they appear in.  COUNT(DISTINCT) is required
        -- rather than tidy: the join fans out per face, so a photo holding two
        -- of a clan's members - or two faces of one person - arrives twice.
        SELECT
            um.category_id,
            COUNT(DISTINCT uf.media_id)::INTEGER AS person_media_count,
            -- the favourites filter is applied a level up, so this only records
            -- whether the category qualifies.  EXISTS rather than a join for the
            -- reason get_person_media gives, and here it is load bearing rather
            -- than defensive: a join that fanned out would corrupt the count
            -- sitting beside it.
            --
            -- the _favorites_only guard is the short circuit that keeps this off
            -- the common path.  AND evaluates left to right, so an unfiltered
            -- request never runs the lookup at all - without it every row of a
            -- plain browse would pay an index probe to answer a question nobody
            -- asked.  BOOL_OR(FALSE) is then FALSE, which the filter above
            -- ignores anyway.
            BOOL_OR(
                _favorites_only
                AND EXISTS (
                    SELECT 1
                    FROM media.favorite fav
                    WHERE fav.media_id = um.media_id
                        AND fav.created_by = _user_id
                )
            ) AS has_favorite_media
        FROM media.user_face uf
        INNER JOIN media.user_media um
            ON um.media_id = uf.media_id
            AND um.user_id = uf.user_id
        INNER JOIN media.person p
            ON p.id = uf.person_id
        WHERE
            uf.user_id = _user_id
            AND (
                (_person_id IS NOT NULL AND uf.person_id = _person_id)
                OR
                -- the clan's ownership is checked here rather than by the caller,
                -- so another user's clan id simply matches nothing and reads as
                -- "no such clan" instead of leaking whose it is
                (_clan_id IS NOT NULL AND uf.person_id IN (
                    SELECT cp.person_id
                    FROM media.clan_person cp
                    INNER JOIN media.clan cl
                        ON cl.id = cp.clan_id
                        AND cl.created_by = _user_id
                    WHERE cp.clan_id = _clan_id
                ))
            )
            AND p.name IS NOT NULL
            AND p.status_code IS NULL
        GROUP BY um.category_id
    ),
    page AS
    (
        SELECT
            v.category_id,
            v.person_media_count,
            c.effective_date AS category_effective_date
        FROM visible v
        INNER JOIN media.category c
            ON c.id = v.category_id
        WHERE
            -- _favorites_only means "favourited either way" here, unlike
            -- get_person_media where only a media favourite exists to test.  the
            -- faces screen drives both views from one toggle, so a caller who
            -- favourited the category itself and a caller who favourited a photo
            -- inside it are both saying this category is one they care about, and
            -- answering only one of them would make the toggle look broken from
            -- whichever side went unanswered.
            --
            -- it filters which categories come back and never
            -- person_media_count, which stays the person's full visible total.
            -- the count is therefore always at least 1: a category is only here
            -- because a photo of theirs put it here.
            --
            -- pushing this test down into the count instead would break that.  a
            -- caller who favourited the category but none of the photos in it
            -- would score 0, and the tile would read "0 photos" while sitting in
            -- the results - so the level this filter sits at is load bearing,
            -- not incidental.
            _favorites_only = FALSE
            OR v.has_favorite_media
            OR EXISTS (
                SELECT 1
                FROM media.category_favorite cfav
                WHERE cfav.category_id = v.category_id
                    AND cfav.created_by = _user_id
            )
        ORDER BY
            -- newest first, matching media.get_categories.  category_id breaks
            -- ties so paging is stable: categories share an effective_date, and
            -- an unordered tie can repeat or skip rows between pages.
            c.effective_date DESC,
            v.category_id
        LIMIT _limit
        OFFSET _offset
    )
    -- the page is taken over categories, not over (category, file) rows.  the
    -- teaser join below fans out a row per file, so a LIMIT applied after it
    -- would slice a teaser in half across a page boundary and hand the client a
    -- category whose teaser is missing some of its scales.
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
        cm.slug AS media_slug,
        md.media_type,
        CASE WHEN f.media_id
            IS NOT NULL THEN true
            ELSE false
            END AS media_is_favorite,
        md.file_id,
        md.file_path,
        md.file_type,
        md.file_scale,
        (
            SELECT ARRAY_AGG(DISTINCT xt.code ORDER BY xt.code)
                FROM media.category_media xcm
                INNER JOIN media.media xm ON xcm.media_id = xm.id
                INNER JOIN media.type xt ON xm.type_id = xt.id
                WHERE xcm.category_id = c.id
        ) AS media_types,
        pg.person_media_count
    FROM page pg
    INNER JOIN media.category c
        ON c.id = pg.category_id
    -- INNER, matching get_categories: a category whose teaser has no files is
    -- not renderable as a tile, so it is left out rather than returned broken
    INNER JOIN media.category_media cm
        ON cm.category_id = c.id
        AND cm.is_teaser = true
    INNER JOIN media.media_detail md
        ON md.media_id = cm.media_id
        AND (
            _exclude_src_files = FALSE
            OR
            md.file_scale <> 'src'
        )
    LEFT OUTER JOIN media.favorite f
        ON f.media_id = cm.media_id
        AND f.created_by = _user_id
    LEFT OUTER JOIN media.category_favorite cf
        ON cf.category_id = c.id
        AND cf.created_by = _user_id
    -- repeated so the file fan-out preserves the page's order
    ORDER BY
        pg.category_effective_date DESC,
        pg.category_id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_person_categories
    TO maw_media;
