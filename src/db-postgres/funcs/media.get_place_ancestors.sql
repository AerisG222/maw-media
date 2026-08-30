-- the chain from a place up to its country, root first.
--
-- the counterpart of media.get_place_descendants, and the breadcrumb a client
-- shows above a drill-in: "United States > Massachusetts > Boston".  media.place
-- stores only parent_id, so without this a client walks the chain itself, which
-- is two extra round trips per drill-in on a tree that is never more than three
-- deep.
--
-- depth is returned so the caller can order without re-deriving it - 1 is the
-- country, and the place asked about is always last.  the result includes
-- _place_id itself, matching get_place_descendants, so a country breadcrumb is
-- one row rather than an empty set.
--
-- no access check.  a place name is not privileged: the caller reached this id
-- through media.get_places, which already applied the rule, and a breadcrumb that
-- said "a place you may not see" would be worse than useless.  the media behind
-- each level is still filtered by media.get_place_media.
--
-- see docs/browse-by-location.md
CREATE OR REPLACE FUNCTION media.get_place_ancestors
(
    _place_id UUID
)
RETURNS TABLE
(
    -- named to avoid postgres substituting an output parameter into a column
    -- reference below, as media.get_place_descendants explains
    ancestor_id UUID,
    ancestor_parent_id UUID,
    ancestor_kind TEXT,
    ancestor_name TEXT,
    ancestor_slug TEXT,
    ancestor_depth INTEGER
)
AS $$
    WITH RECURSIVE chain AS
    (
        SELECT p.id, p.parent_id, p.kind, p.name, p.slug, 0 AS rung
        FROM media.place p
        WHERE p.id = _place_id

        UNION ALL

        SELECT p.id, p.parent_id, p.kind, p.name, p.slug, c.rung - 1
        FROM media.place p
        INNER JOIN chain c
            ON p.id = c.parent_id
    )
    SELECT
        id,
        parent_id,
        kind,
        name,
        slug,
        -- rung counts down from the place, so the country is the most negative;
        -- shifting by the minimum turns it into a 1-based depth from the root
        (rung - MIN(rung) OVER () + 1)::INTEGER
    FROM chain
    ORDER BY rung;
$$ LANGUAGE sql STABLE;

GRANT EXECUTE
    ON FUNCTION media.get_place_ancestors
    TO maw_media;
