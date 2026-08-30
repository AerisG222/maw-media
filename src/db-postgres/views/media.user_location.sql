-- which media at which places a user may see.
--
-- the location half of media.user_face: access is inherited rather than restated.
-- a media at a place is visible when the media itself is, and the definition of
-- that - category_role -> user_role -> category_media - lives in
-- media.user_category.  every place read path goes through this view rather than
-- repeating the rule and hoping each copy got it right.
--
-- it composes media.user_category with media.category_media directly, where
-- media.user_face reaches for media.user_media instead.  the difference is not a
-- shortcut around the access rule: media.user_media *is* those same two relations
-- with a DISTINCT over (category_id, media_id, media_slug, user_id), and this view
-- uses neither category_id nor media_slug.  going through it would buy a dedupe
-- of columns that are then projected away, at the cost of an optimization barrier
-- - the DISTINCT materializes all 167,202 rows and spills to disk before any
-- place predicate can prune it.  measured on the phase 0 dev restore, composing
-- directly runs a city page in 103ms against 297ms and the country counts in
-- 222ms against 403ms, and the two forms were verified to return identical sets
-- across all 2,937,290 rows.
--
-- category_id is deliberately absent, for exactly the reason media.user_face
-- gives.  a media sitting in two categories a user can see would arrive twice;
-- carrying that through would double count every media in an aggregate - and
-- media_count is the number this view exists to feed.  callers that need the
-- category (media.get_place_categories, which rolls places up to the categories
-- holding them) join media.user_media separately, which keeps that intent
-- explicit rather than accidental.
--
-- DISTINCT is what makes that safe, and it is defensive rather than currently
-- load bearing - worth saying plainly so nobody removes it after measuring that
-- it changes nothing.  every media in the library today belongs to exactly one
-- category (167,202 category_media rows over 167,202 distinct media), so there is
-- presently no duplication to absorb.  the schema permits it, and media.user_face
-- carries the same guard, so this holds the line for the day a media is filed in
-- two places.  the other fan-out source - a category reachable through two of a
-- user's roles - is absorbed upstream by media.user_category's own DISTINCT,
-- which matters, because media.category_role averages two roles per category.
--
-- place_id is the *deepest* place the media's coordinate resolved to - a city
-- where there was one, otherwise a state or a country.  a caller asking for a
-- country's media therefore cannot match on place_id alone; it expands the place
-- to its descendants first, which is what media.get_place_descendants is for.
--
-- a coordinate with no place - never geocoded, or geocoded without a country - is
-- excluded, so it is not browsable by location.  that is the whole access story
-- for those 2,171 rows: they are not hidden, there is simply nowhere to file them.
--
-- see docs/browse-by-location.md
CREATE OR REPLACE VIEW media.user_location AS
    SELECT DISTINCT
        uc.user_id,
        l.place_id,
        cm.media_id
    FROM media.user_category uc
    INNER JOIN media.category_media cm
        ON cm.category_id = uc.category_id
    INNER JOIN media.media_location ml
        ON ml.media_id = cm.media_id
    INNER JOIN media.location l
        ON l.id = ml.location_id
    WHERE l.place_id IS NOT NULL;

GRANT SELECT
ON media.user_location
TO maw_media;
