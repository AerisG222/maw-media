-- 2025-11-04 - add slug to return
DROP FUNCTION IF EXISTS media.get_random_media;

CREATE OR REPLACE FUNCTION media.get_random_media
(
    _user_id UUID,
    _count SMALLINT = 1,
    _exclude_src_files BOOLEAN = False
)
RETURNS TABLE
(
    category_id UUID,
    category_year SMALLINT,
    category_slug TEXT,
    media_id UUID,
    media_slug TEXT,
    media_type TEXT,
    media_is_favorite BOOLEAN,
    file_id UUID,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT
)
AS $$
DECLARE
    -- the picks, as two parallel arrays rather than one array of pairs: a
    -- composite type would have to be declared at schema level for a value that
    -- never leaves this function.  UNNEST takes both at once below and keeps them
    -- paired, which is the only property needed.
    _category_ids UUID[];
    _media_ids UUID[];
    _sample_pct NUMERIC;
BEGIN

    IF _count < 1 THEN
        _count := 1;
    ELSIF _count > 100 THEN
        _count := 100;
    END IF;

    -- 2026-09-20 - sample first, and only enumerate everything if that fails.
    --
    -- ORDER BY RANDOM() has to produce every row the caller can see before it can
    -- discard all but _count of them: 169,121 rows to answer "show me one photo".
    -- TABLESAMPLE reads a random selection of *blocks* instead, so the work is
    -- proportional to the sample rather than to the library.
    --
    -- the sample is sized from the planner's own row estimate, which is a catalog
    -- lookup rather than a count, and aims for roughly 25,000 rows.  that is ~15%
    -- of the table today and it tracks the table as it grows - the number that
    -- matters is how many rows come back, not what fraction they are.
    SELECT LEAST(100, GREATEST(1, 25000.0 * 100.0 / GREATEST(c.reltuples, 1)))
    INTO _sample_pct
    FROM pg_class c
    WHERE c.oid = 'media.category_media'::REGCLASS;

    -- why 25,000 and not the few rows actually wanted: a block holds ~61 rows of
    -- this table, and the table is physically clustered by category, so rows drawn
    -- from one block tend to share a category.  a sample that is too small
    -- therefore returns a "random" page that is really two or three events.
    -- measured over 24 picks against the full sort's 24 distinct categories: a
    -- 2,000 row sample gave 16-20, and 25,000 gives 23-24.
    --
    -- 2026-10-06 - access is decided by media.user_media, in two passes.
    --
    -- it has to be media.user_media rather than media.user_category alone: the
    -- sample is drawn per (category, media) row, and a media may now be restricted
    -- to fewer roles than its category grants, so a category level check would
    -- hand a restricted photo to anybody who can see the category around it.
    --
    -- but asking media.user_media about the whole sample is what it cannot do
    -- cheaply.  postgres estimates media.user_category at ~118 rows where an admin
    -- has 2,170, so it judges hashing all of the caller's media.user_media to be
    -- small, and builds a 170,376 row hash to test 25,000 sampled rows - 60ms
    -- against the 15ms this function took with a category check.
    --
    -- so the inner pass prunes on category, which is cheap and can only *remove*
    -- rows - no media is visible inside a category that is not - and takes a few
    -- times _count of the survivors at random.  the outer pass then puts those
    -- few through media.user_media, which stays the only authority on what may be
    -- returned.  the category check is a filter in front of the rule, not a
    -- second copy of it.
    --
    -- four times _count is headroom for restricted media, which are rare - a few
    -- photos hidden from one role.  a caller who loses more than that to
    -- restrictions comes back short, and the fallback below takes over, exactly as
    -- it does for a caller who can see too little of the library to sample.
    SELECT ARRAY_AGG(s.category_id), ARRAY_AGG(s.media_id)
    INTO _category_ids, _media_ids
    FROM
    (
        SELECT
            c.category_id,
            c.media_id
        FROM
        (
            SELECT
                cm.category_id,
                cm.media_id
            FROM media.category_media cm TABLESAMPLE SYSTEM (_sample_pct)
            WHERE EXISTS
            (
                SELECT 1
                FROM media.user_category uc
                WHERE uc.category_id = cm.category_id
                    AND uc.user_id = _user_id
            )
            ORDER BY RANDOM()
            LIMIT _count * 4
        ) c
        WHERE EXISTS
        (
            SELECT 1
            FROM media.user_media um
            WHERE um.category_id = c.category_id
                AND um.media_id = c.media_id
                AND um.user_id = _user_id
        )
        LIMIT _count
    ) s;

    -- the sample knows nothing about who is asking, so a caller who can see a
    -- small enough slice of the library may find little or none of it in one: a
    -- user with 50 of these 169,121 rows drew 3 from a 15% sample.  falling back
    -- to the exact query keeps the contract - _count rows whenever _count rows
    -- exist - and costs that caller what this function cost everybody before.
    --
    -- it is also the path a small library takes: the sample rounds up to 100% when
    -- the table is small, so the two agree there rather than differing by size.
    IF COALESCE(ARRAY_LENGTH(_media_ids, 1), 0) < _count THEN

        SELECT ARRAY_AGG(s.category_id), ARRAY_AGG(s.media_id)
        INTO _category_ids, _media_ids
        FROM
        (
            SELECT
                um.category_id,
                um.media_id
            FROM media.user_media um
            WHERE um.user_id = _user_id
            ORDER BY RANDOM()
            LIMIT _count
        ) s;

    END IF;

    -- a caller who can see nothing at all: ARRAY_AGG over no rows is null, and
    -- UNNEST of a null array would raise rather than return empty
    IF _media_ids IS NULL THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT
        picked.category_id,
        c.year AS category_year,
        c.slug AS category_slug,
        md.media_id,
        cm.slug AS media_slug,
        md.media_type,
        CASE WHEN f.media_id
            IS NOT NULL THEN true
            ELSE false
            END AS media_is_favorite,
        md.file_id,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM UNNEST(_category_ids, _media_ids) AS picked(category_id, media_id)
    -- the slug the pick was made on, read back by primary key rather than carried
    -- through a third array
    INNER JOIN media.category_media cm
        ON cm.category_id = picked.category_id
        AND cm.media_id = picked.media_id
    INNER JOIN media.category c
        ON c.id = picked.category_id
    INNER JOIN media.media_detail md
        ON md.media_id = picked.media_id
        AND (
            _exclude_src_files = FALSE
            OR
            md.file_scale <> 'src'
        )
    LEFT OUTER JOIN media.favorite f
        ON f.media_id = picked.media_id
        AND f.created_by = _user_id;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_random_media
    TO maw_media;
