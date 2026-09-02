-- folds one place into another and deletes it.
--
-- the fix for a place the geocoder spelled two ways.  the audit found 'China ->
-- Guangdong' sitting beside 'China -> Guangdong Sheng': one real province, two
-- names, and no normalizer can reconcile them without guessing - which is why
-- media.place_alias exists and why an admin has to say so explicitly.
--
-- everything pointing at the loser is repointed at the winner and the loser row
-- is removed.  the aliases move with it, and that is what makes the merge
-- durable: the next derivation pass still looks up ('state', China, 'guangdong
-- sheng'), still finds an alias, and now resolves to the winner.  without that
-- the pass would faithfully recreate the place that was just merged away, and the
-- merge would look like it had silently failed.
--
-- both places must be the same kind, but they need not share a parent - and that
-- matters more than it sounds.  the audit's 'United States -> Acton' looked like a
-- re-parenting job, but there was already a 'Massachusetts -> Acton' holding 3,675
-- locations: one town the geocoder filed twice, once with a state and once
-- without.  merging is the correct fix there, and re-parenting would instead have
-- left two Actons under Massachusetts with a suffixed slug.
--
-- merging across kinds is refused.  a state folded into a country is not a
-- correction but a different mistake, and the level rule that guards
-- media.set_place_parent would have to be re-reasoned for every child moved.
--
-- the loser's cover, if it had one, goes with the row.  had_cover is returned so
-- the caller can delete the published file - the database cannot reach the
-- filesystem, and a cover file that outlives its place is served to anyone who
-- guesses the id.
--
-- return codes:
--   0  merged
--   1  caller is not an admin
--   2  no such winner
--   3  no such loser
--   4  they are the same place
--   5  the two are different kinds
--   6  the winner sits beneath the loser, so the merge would create a cycle.
--      unreachable while levels are unique per kind; see the check itself
--
-- see docs/browse-by-location.md
CREATE OR REPLACE FUNCTION media.merge_places
(
    _user_id UUID,
    _winner_id UUID,
    _loser_id UUID,
    OUT result INTEGER,
    OUT had_cover BOOLEAN,
    OUT moved_locations INTEGER,
    OUT moved_children INTEGER
)
AS $$
DECLARE
    winner_kind TEXT;
    loser_kind TEXT;
BEGIN
    had_cover := FALSE;
    moved_locations := 0;
    moved_children := 0;

    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RAISE NOTICE 'not authorized - user % is not an admin!', _user_id;
        result := 1;
        RETURN;
    END IF;

    IF _winner_id = _loser_id THEN
        result := 4;
        RETURN;
    END IF;

    SELECT p.kind INTO winner_kind FROM media.place p WHERE p.id = _winner_id;
    SELECT p.kind, (p.cover_media_id IS NOT NULL)
    INTO loser_kind, had_cover
    FROM media.place p WHERE p.id = _loser_id;

    IF winner_kind IS NULL THEN
        result := 2;
        RETURN;
    END IF;

    IF loser_kind IS NULL THEN
        result := 3;
        RETURN;
    END IF;

    IF winner_kind <> loser_kind THEN
        result := 5;
        RETURN;
    END IF;

    -- the loser's children are about to become the winner's.  if the winner is one
    -- of them it would end up its own ancestor.  defensive rather than reachable:
    -- the two are the same kind and therefore the same level, and a descendant
    -- always sits strictly below its ancestor, so neither can contain the other.
    -- it stays because media.get_place_descendants recurses without a depth bound,
    -- and a cycle would hang the browse rather than merely look wrong.
    IF EXISTS (
        SELECT 1
        FROM media.get_place_descendants(_loser_id) d
        WHERE d.descendant_id = _winner_id
    ) THEN
        result := 6;
        RETURN;
    END IF;

    -- an alias the winner already holds under the same (kind, parent, key) would
    -- violate the unique index once repointed.  it says nothing the winner's own
    -- alias does not already say, so it is dropped rather than merged.
    DELETE FROM media.place_alias loser_alias
    WHERE loser_alias.place_id = _loser_id
        AND EXISTS (
            SELECT 1
            FROM media.place_alias winner_alias
            WHERE winner_alias.place_id = _winner_id
                AND winner_alias.kind = loser_alias.kind
                AND winner_alias.parent_place_id IS NOT DISTINCT FROM loser_alias.parent_place_id
                AND winner_alias.match_key = loser_alias.match_key
        );

    -- what the geocoder produced for the loser now resolves to the winner.  the
    -- (kind, parent, match_key) tuple is left exactly as it was: it records what
    -- the geocoder says, not where an admin decided the place belongs.
    UPDATE media.place_alias
    SET place_id = _winner_id
    WHERE place_id = _loser_id;

    -- aliases that hung *beneath* the loser have to follow too, or the delete
    -- below would violate their foreign key
    UPDATE media.place_alias
    SET parent_place_id = _winner_id
    WHERE parent_place_id = _loser_id;

    UPDATE media.place
    SET parent_id = _winner_id,
        modified = NOW()
    WHERE parent_id = _loser_id;

    GET DIAGNOSTICS moved_children = ROW_COUNT;

    UPDATE media.location
    SET place_id = _winner_id
    WHERE place_id = _loser_id;

    GET DIAGNOSTICS moved_locations = ROW_COUNT;

    DELETE FROM media.place WHERE id = _loser_id;

    result := 0;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.merge_places
    TO maw_media;
