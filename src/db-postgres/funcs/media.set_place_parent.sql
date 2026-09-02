-- moves a place to a different parent.
--
-- the fix for a place the geocoder filed in the wrong branch.  the audit found
-- three: 'United States -> Acton' and two Thai cities that parented straight to
-- their country because the geocode came back with no state at all, so
-- media.assign_location_place had nothing to hang them from.
--
-- the aliases are deliberately left alone, and that is the whole subtlety here.
-- media.place_alias records what the *geocoder* produces - ('city', United
-- States, 'acton') for those two coordinates, because their state really is null
-- - while media.place.parent_id records where an admin decided it belongs.  after
-- this runs the two disagree, on purpose: the next derivation pass still looks up
-- ('city', United States, 'acton'), still finds that alias, and still resolves to
-- the same place, which now hangs under Massachusetts.  repointing the alias to
-- the new parent would break exactly that - the lookup would miss and a second
-- Acton would be created under the country on the next pass, silently undoing the
-- correction.
--
-- _new_parent_id may be NULL, which makes the place a root.  only the shallowest
-- kind may sit there, so a city cannot be promoted to the top of the tree.
--
-- return codes:
--   0  moved
--   1  caller is not an admin
--   2  no such place
--   3  no such parent
--   4  the parent does not sit above the place in the hierarchy
--   5  the parent is the place itself or one of its descendants (a cycle).
--      unreachable while levels are unique per kind; see the check itself
--   6  only a country may be a root
--
-- see docs/browse-by-location.md
CREATE OR REPLACE FUNCTION media.set_place_parent
(
    _user_id UUID,
    _place_id UUID,
    _new_parent_id UUID
)
RETURNS INTEGER
AS $$
DECLARE
    place_kind TEXT;
    place_level SMALLINT;
    place_slug TEXT;
    parent_level SMALLINT;
    root_level SMALLINT;
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RAISE NOTICE 'not authorized - user % is not an admin!', _user_id;
        RETURN 1;
    END IF;

    SELECT p.kind, k.level, p.slug
    INTO place_kind, place_level, place_slug
    FROM media.place p
    INNER JOIN media.place_kind k ON k.code = p.kind
    WHERE p.id = _place_id;

    IF place_kind IS NULL THEN
        RETURN 2;
    END IF;

    IF _new_parent_id IS NULL THEN
        SELECT MIN(level) INTO root_level FROM media.place_kind;

        IF place_level <> root_level THEN
            RETURN 6;
        END IF;
    ELSE
        SELECT k.level
        INTO parent_level
        FROM media.place p
        INNER JOIN media.place_kind k ON k.code = p.kind
        WHERE p.id = _new_parent_id;

        IF parent_level IS NULL THEN
            RETURN 3;
        END IF;

        -- the invariant media.place cannot express as a CHECK, because it spans
        -- rows: a parent sits at a strictly lower level than its child, which is
        -- what keeps a city from ending up beneath another city
        IF parent_level >= place_level THEN
            RETURN 4;
        END IF;

        -- a place cannot be moved beneath itself.  this is defensive rather than
        -- reachable today: levels are unique per kind and a parent must sit
        -- strictly above its child, so any descendant already has a higher level
        -- and the check above rejects it first.  it stays because
        -- media.get_place_descendants recurses without a depth bound, so if the
        -- level rule is ever relaxed - two kinds sharing a level, say - a cycle
        -- would hang the browse rather than merely look wrong.
        IF _new_parent_id = _place_id
            OR EXISTS (
                SELECT 1
                FROM media.get_place_descendants(_place_id) d
                WHERE d.descendant_id = _new_parent_id
            )
        THEN
            RETURN 5;
        END IF;
    END IF;

    -- slugs are unique per parent, so a move can collide with a sibling that was
    -- already there.  suffixing with the head of the id resolves it in one probe
    -- and cannot itself collide - the same rule media.resolve_place applies when
    -- it creates a place.
    IF EXISTS (
        SELECT 1
        FROM media.place p
        WHERE p.parent_id IS NOT DISTINCT FROM _new_parent_id
            AND p.slug = place_slug
            AND p.id <> _place_id
    ) THEN
        place_slug := place_slug || '-' || LEFT(_place_id::TEXT, 8);
    END IF;

    UPDATE media.place
    SET parent_id = _new_parent_id,
        slug = place_slug,
        modified = NOW()
    WHERE id = _place_id;

    RETURN 0;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.set_place_parent
    TO maw_media;
