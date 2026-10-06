-- which media at which places a user may see.
--
-- the location half of media.user_face: access is inherited rather than restated.
-- a media at a place is visible when the media itself is, and the definition of
-- that lives in media.user_media.  every place read path goes through this view
-- rather than repeating the rule and hoping each copy got it right.
--
-- 2026-10-06 - built on media.user_media, as media.user_face is.  it used to
-- compose media.user_category with media.category_media itself, because
-- media.user_media then carried a DISTINCT that blocked every place predicate
-- from reaching inside it - 297ms against 103ms for a city page on the phase 0
-- restore.  that DISTINCT was dropped on 2026-09-20 for never having removed a
-- row, so going through the view now costs nothing, and the reason to restate
-- the rule here went with it.
--
-- and the reason *not* to restate it arrived: a media can now be restricted to
-- fewer roles than its category grants.  that is decided per media, so it lives
-- in media.user_media, and a view composing category level access for itself
-- would quietly file a photo under a place for a caller forbidden to see it.
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
        um.user_id,
        l.place_id,
        um.media_id
    FROM media.user_media um
    INNER JOIN media.media_location ml
        ON ml.media_id = um.media_id
    INNER JOIN media.location l
        ON l.id = ml.location_id
    WHERE l.place_id IS NOT NULL;

GRANT SELECT
ON media.user_location
TO maw_media;
