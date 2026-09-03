DROP FUNCTION IF EXISTS media.get_media_places;

-- where one media was taken, as the chain of places holding it: country first,
-- then its state and city where the geocoder resolved that deep.
--
-- the reverse of every other read here, and it exists for one screen: an admin
-- looking at a photograph and deciding it should represent the place it was taken
-- at.  without it a client holding a media id has no way to name a place id, and
-- media.set_place_cover takes place ids.
--
-- returns whole Place rows rather than the breadcrumb media.get_place_ancestors
-- gives, because that screen needs each rung's *current* cover to show what would
-- be replaced - and the count, to say how much the choice speaks for.  it gets
-- them by calling media.get_places once per rung rather than restating its
-- aggregate, so the covers, counts and ancestor names here can never disagree
-- with the ones the browse shows.  the tree is three deep, so that is at most
-- three single-place aggregates.
--
-- the access rule is inherited twice over and never restated: media.user_location
-- yields nothing for a media the caller cannot see, and media.get_places drops a
-- rung the caller may see nothing at.  so this cannot be used to discover that a
-- place exists, nor that a media does.
--
-- a media with no location, or one whose coordinate was never geocoded, returns
-- no rows - the same "nowhere to file it" media.user_location applies, and an
-- empty answer is the honest one.
--
-- see docs/browse-by-location.md
CREATE OR REPLACE FUNCTION media.get_media_places
(
    _user_id UUID,
    _media_id UUID
)
RETURNS TABLE
(
    id UUID,
    parent_id UUID,
    kind TEXT,
    name TEXT,
    slug TEXT,
    media_count INTEGER,
    cover_created TIMESTAMPTZ,
    cover_media_id UUID,
    ancestor_names TEXT[],
    -- always 0 for the deepest rung and usually not for the ones above it. this
    -- function returns media.get_places rows verbatim, so its shape is that
    -- function's shape - a column added there has to be added here too, or
    -- postgres refuses the mismatch at call time
    child_count INTEGER
)
AS $$
DECLARE
    _place_id UUID;
BEGIN
    -- the deepest place the media's coordinate resolved to.  one row per
    -- (user, place, media) by construction, so LIMIT 1 guards a duplicate that
    -- cannot presently arise rather than choosing between real alternatives.
    SELECT ul.place_id
    INTO _place_id
    FROM media.user_location ul
    WHERE ul.media_id = _media_id
        AND ul.user_id = _user_id
    LIMIT 1;

    IF _place_id IS NULL THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT p.*
    FROM media.get_place_ancestors(_place_id) a
    CROSS JOIN LATERAL media.get_places(_user_id, NULL, NULL, a.ancestor_id, NULL) p
    -- country first, matching the breadcrumb and letting a client render the
    -- rungs in the order it was handed them
    ORDER BY a.ancestor_depth;
END;
$$ LANGUAGE plpgsql STABLE;

GRANT EXECUTE
    ON FUNCTION media.get_media_places
    TO maw_media;
