-- a place and everything beneath it.
--
-- media.location.place_id records only the *deepest* place a coordinate resolved
-- to, so a photo taken in Boston is filed against the city and not against
-- Massachusetts or the United States.  a caller asking for a country's media
-- therefore cannot match on place_id directly - it has to expand the place to its
-- subtree first, and this is the one definition of that expansion.  it exists for
-- the reason media.get_visible_person_count does: so the place read functions
-- share a definition rather than each restating the walk.
--
-- recursive rather than a closure table because the tree is tiny and shallow -
-- the phase 0 audit measured 8 countries, 31 states and 239 cities, three levels
-- deep - so the walk costs less than maintaining a materialization of it would.
--
-- the result always includes _place_id itself, which is what makes a city (a leaf)
-- and a country (a root) the same query to the caller.

--
-- see docs/browse-by-location.md
CREATE OR REPLACE FUNCTION media.get_place_descendants
(
    _place_id UUID
)
RETURNS TABLE
(
    -- deliberately not named place_id: postgres substitutes a sql function's
    -- named output parameters into its body, so a name matching a column of
    -- media.place would make every reference below ambiguous
    descendant_id UUID
)
AS $$
    WITH RECURSIVE descendants AS
    (
        SELECT p.id
        FROM media.place p
        WHERE p.id = _place_id

        UNION ALL

        SELECT c.id
        FROM media.place c
        INNER JOIN descendants d
            ON c.parent_id = d.id
    )
    SELECT id
    FROM descendants;
$$ LANGUAGE sql STABLE;

GRANT EXECUTE
    ON FUNCTION media.get_place_descendants
    TO maw_media;
