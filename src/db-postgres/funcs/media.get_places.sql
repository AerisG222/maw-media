-- the places a user may browse at one level of the hierarchy.
--
-- the drill-down: with no _parent_id it lists the countries, and with one it lists
-- that place's children.  a country's children are usually its states, but not
-- always - Macao and Hong Kong have no state level, so their cities parent
-- straight to the country and a single listing can mix kinds.  that is why `kind`
-- is returned rather than implied by the depth of the request.
--
-- returned whole rather than paged, like media.get_persons, and for a stronger
-- reason: every request is scoped to one parent, so the result is one country's
-- states or one state's cities.  the phase 0 audit measured 8 countries, 31 states
-- and 239 cities across the whole library - there is no request here that returns
-- "every city", so there is nothing to page.
--
-- media_count is computed per caller through media.user_location, and counts the
-- place's whole subtree: a country's count includes every photo in its states and
-- their cities, not just the handful whose coordinates resolved no deeper than the
-- country itself.
--
-- the join to media.user_location is INNER, and that is the entire access rule: a
-- place holding nothing the caller may see does not appear at all.  so a user
-- restricted out of every category from a trip does not learn the trip happened.
-- it also keeps the list honest - a tile that reads "0 photos" and leads to an
-- empty screen is worse than no tile.
--
-- _place_id narrows to a single place without changing any of the above, which is
-- the trick media.get_persons uses: a caller holding an id gets the same shape and
-- the same access rule rather than a second function that could drift from this
-- one.  it ignores _parent_id when supplied.
--
-- there is deliberately no teaser image here.  a teaser drawn from the caller's
-- own media would have to be chosen per user - it must be a photo they can see -
-- which means a LATERAL join per tile; measured on the dev restore that took a
-- city listing from 144ms to 991ms.  the intent instead is a curated image *of*
-- the place, which is identical for every caller and can therefore be a plain
-- column on media.place, read with no extra work at query time.  it is additive
-- when it arrives: a new field beside these, not a change to any of them.
--
-- see docs/browse-by-location.md
CREATE OR REPLACE FUNCTION media.get_places
(
    _user_id UUID,
    _parent_id UUID = NULL,
    _kind TEXT = NULL,
    _place_id UUID = NULL
)
RETURNS TABLE
(
    id UUID,
    parent_id UUID,
    kind TEXT,
    name TEXT,
    slug TEXT,
    media_count INTEGER
)
AS $$
BEGIN
    RETURN QUERY
    WITH RECURSIVE candidate AS
    (
        -- the places this request is about, before any counting
        SELECT p.id
        FROM media.place p
        WHERE
            p.is_hidden = FALSE
            AND (_kind IS NULL OR p.kind = _kind)
            AND (
                CASE
                    WHEN _place_id IS NOT NULL THEN p.id = _place_id
                    -- IS NOT DISTINCT FROM rather than =, because a null
                    -- _parent_id means "the root" and every country's parent_id is
                    -- null - plain equality would match nothing and the top level
                    -- of the browse would come back empty
                    ELSE p.parent_id IS NOT DISTINCT FROM _parent_id
                END
            )
    ),
    subtree AS
    (
        -- each candidate paired with itself and everything beneath it, so one pass
        -- counts every level at once.  media.get_place_descendants does this for a
        -- single place; inlining it here avoids calling it once per candidate.
        SELECT c.id AS root_id, c.id AS node_id
        FROM candidate c

        UNION ALL

        SELECT s.root_id, ch.id
        FROM media.place ch
        INNER JOIN subtree s
            ON ch.parent_id = s.node_id
    )
    SELECT
        p.id,
        p.parent_id,
        p.kind,
        p.name,
        p.slug,
        COUNT(DISTINCT ul.media_id)::INTEGER
    FROM subtree s
    INNER JOIN media.place p
        ON p.id = s.root_id
    INNER JOIN media.user_location ul
        ON ul.place_id = s.node_id
        AND ul.user_id = _user_id
    GROUP BY
        p.id,
        p.parent_id,
        p.kind,
        p.name,
        p.slug
    ORDER BY
        -- busiest first, matching how media.get_persons leads with the people a
        -- caller actually looks for.  name breaks ties so the order is stable
        -- between calls rather than left to the aggregate.
        COUNT(DISTINCT ul.media_id) DESC,
        p.name ASC;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_places
    TO maw_media;
