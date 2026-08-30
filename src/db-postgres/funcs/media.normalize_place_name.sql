-- the match key for one level of a reverse geocoded address.
--
-- lowercased, with runs of whitespace collapsed and the ends trimmed.  that is
-- the whole of it: the phase 0 audit found country and state names arrive
-- consistently long form ('United States', 'Massachusetts') and collide nowhere
-- after this treatment, so anything more aggressive would be solving a problem
-- the data does not have.  the genuinely divergent spellings that do exist -
-- 'Guangdong' beside 'Guangdong Sheng' - are not something a normalizer can
-- reconcile without guessing; media.place_alias exists so an admin can say so
-- explicitly.
--
-- no unaccent.  the audit found exactly one non-ascii value in 27,870 rows (a
-- Thai state name, which unaccent does not transliterate anyway) and not a single
-- accented latin character, so the extension would earn nothing.
--
-- returns NULL for a value that is absent, empty or whitespace only, so callers
-- get one answer for "this level is missing" rather than three.  the audit found
-- no empty strings today; this keeps it that way if the geocoder ever changes.
CREATE OR REPLACE FUNCTION media.normalize_place_name
(
    _value TEXT
)
RETURNS TEXT
AS $$
    SELECT NULLIF(LOWER(BTRIM(REGEXP_REPLACE(_value, '\s+', ' ', 'g'))), '');
$$ LANGUAGE sql IMMUTABLE STRICT;

GRANT EXECUTE
    ON FUNCTION media.normalize_place_name
    TO maw_media;
