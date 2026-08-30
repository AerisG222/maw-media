-- the url fragment for a place.
--
-- cosmetic, and deliberately so: every route keys on the place's uuid, exactly
-- as the person routes key on {id:guid}.  a slug that reads badly is a display
-- wart, never a broken link, which is what lets the fallback below be as blunt as
-- it is.
--
-- _id is taken rather than derived because a name can slugify to nothing at all.
-- the audit's one non-ascii value - the Thai กรุงเทพมหานคร - has no character in
-- [a-z0-9], so it reduces to an empty string; falling back to the head of the
-- uuid keeps the result non-empty and unique without inventing a transliteration
-- scheme for a single row.
--
-- two shaping rules beyond the obvious lowercasing:
--
--   * periods and apostrophes are removed rather than turned into separators, so
--     'Washington D.C.' slugs as 'washington-dc' and 'St. John''s' as
--     'st-johns', rather than 'washington-d-c' and 'st-john-s'.  only the ascii
--     apostrophe is covered - the geocoder has never returned the typographic
--     one (the audit found a single non-ascii value in 27,870 rows, a Thai state
--     name), and one would slug as a dash like any other separator
--   * a run of anything else non-alphanumeric collapses to one dash, and the ends
--     are trimmed, so '  --Hong  Kong--  ' slugs as 'hong-kong'.  the '+' on the
--     character class is what does the collapsing - there is no second pass
--
-- note this does not guarantee uniqueness on its own.  two distinct names under
-- one parent can slugify alike ('St. John' and 'St John' normalize differently,
-- so they are two places, but they slugify identically), and the per-parent
-- unique index on media.place would reject the second.  media.resolve_place owns
-- that collision, since only it knows what is already taken.
CREATE OR REPLACE FUNCTION media.build_place_slug
(
    _name TEXT,
    _id UUID
)
RETURNS TEXT
AS $$
    SELECT COALESCE(
        NULLIF(
            BTRIM(
                REGEXP_REPLACE(
                    -- periods and apostrophes are dropped rather than separated,
                    -- so the word closes up the way a reader expects: 'U.S.A.' is
                    -- one word abbreviated and 'St. John''s' is two, whereas
                    -- 'u-s-a' and 'st-john-s' read as three.  TRANSLATE with an
                    -- empty target deletes every character in the set in one
                    -- pass.  it has to run before the pass below, which would
                    -- otherwise have already turned both into dashes.
                    TRANSLATE(LOWER(_name), '.''', ''),
                    '[^a-z0-9]+', '-', 'g'
                ),
                '-'
            ),
            ''
        ),
        LEFT(_id::TEXT, 8)
    );
$$ LANGUAGE sql IMMUTABLE STRICT;

GRANT EXECUTE
    ON FUNCTION media.build_place_slug
    TO maw_media;
