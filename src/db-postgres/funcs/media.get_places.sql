-- 2026-09-01 - add cover_created (the cover url is derived from place id)
-- 2026-09-02 - add _search, and return cover_media_id and ancestor_names
DROP FUNCTION IF EXISTS media.get_places;

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
-- place holding nothing the caller may see does not appear at all.  it doubles as
-- the tidying rule - a place emptied by a merge or a re-parent stops being listed
-- without anything having to mark it, which is why media.place carries no hidden
-- flag.  so a user
-- restricted out of every category from a trip does not learn the trip happened.
-- it also keeps the list honest - a tile that reads "0 photos" and leads to an
-- empty screen is worse than no tile.
--
-- there are three ways to ask, and they take precedence in this order:
--
--   _place_id   one place, by id.  the trick media.get_persons uses: a caller
--               holding an id gets the same shape and the same access rule rather
--               than a second function that could drift from this one.
--   _search     every place whose name contains the term, anywhere in the tree.
--               the admin corrections need this - merging two places means finding
--               the second one, and drilling to it requires already knowing where
--               it is, which is exactly what is not known when hunting duplicates.
--   _parent_id  the children of one place, or the countries when it is null.
--
-- a supplied _place_id or _search makes _parent_id irrelevant, so search is
-- deliberately whole-tree rather than scoped to a branch: the duplicates worth
-- finding sit in *different* branches, which is what makes them hard to spot.
--
-- _kind still applies in every mode, so "every city called Zhuhai" is one call.
--
-- matching is on the same normalized form derivation uses, via strpos rather than
-- LIKE so a term containing % or _ is a literal rather than a pattern.  a search
-- that normalizes to nothing - empty, or only whitespace - falls through to the
-- parent listing rather than matching everything.
--
-- cover_created is a plain column read, not a teaser computed per caller.  a teaser
-- drawn from the caller's own media would have to be chosen per user - it must be
-- a photo they can see - which means a LATERAL join per tile; measured on the dev
-- restore that took a city listing from 144ms to 991ms.  an admin's hand picked
-- cover is the same image for everybody, so it costs nothing here.
--
-- it is returned to every caller regardless of what they can see, which is
-- correct rather than a leak: the file it names is served to any signed in caller
-- without a per file check, so withholding the name would protect nothing.  what
-- makes that safe is the choosing, not the reading - see media.set_place_cover.
--
-- see docs/browse-by-location.md
CREATE OR REPLACE FUNCTION media.get_places
(
    _user_id UUID,
    _parent_id UUID = NULL,
    _kind TEXT = NULL,
    _place_id UUID = NULL,
    _search TEXT = NULL
)
RETURNS TABLE
(
    id UUID,
    parent_id UUID,
    kind TEXT,
    name TEXT,
    slug TEXT,
    media_count INTEGER,
    -- null when there is no cover.  the file name is derived from id, so this
    -- carries only whether one exists and which version, for the url's ?v=
    cover_created TIMESTAMPTZ,
    -- which photograph the cover was published from.  an admin screen needs it to
    -- show the current choice as selected in the picker it offers, which it cannot
    -- do from the url alone - that names the published copy, not the original.
    cover_media_id UUID,
    -- the names above this place, root first, excluding itself.
    --
    -- it exists because search results are otherwise ambiguous, and not merely
    -- inconveniently so: this library holds two cities called Zhuhai, both under a
    -- parent called Guangdong, and only the grandparent - China against Macao -
    -- tells them apart.  a result carrying just parent_id would leave an admin
    -- unable to say which one they were about to merge.
    --
    -- an array rather than a joined string, matching how media.get_categories
    -- returns media_types, so the client picks its own separator.
    ancestor_names TEXT[]
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
            (_kind IS NULL OR p.kind = _kind)
            AND (
                CASE
                    WHEN _place_id IS NOT NULL THEN p.id = _place_id
                    WHEN media.normalize_place_name(_search) IS NOT NULL THEN
                        STRPOS(
                            media.normalize_place_name(p.name),
                            media.normalize_place_name(_search)
                        ) > 0
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
        COUNT(DISTINCT ul.media_id)::INTEGER,
        p.cover_created,
        p.cover_media_id,
        -- reuses the one definition of the chain rather than restating the walk.
        -- the tree is three deep and a few hundred wide, so a call per row costs
        -- nothing worth measuring.
        (
            SELECT ARRAY_AGG(a.ancestor_name ORDER BY a.ancestor_depth)
            FROM media.get_place_ancestors(p.id) a
            WHERE a.ancestor_id <> p.id
        )
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
        p.slug,
        p.cover_created,
        p.cover_media_id
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
