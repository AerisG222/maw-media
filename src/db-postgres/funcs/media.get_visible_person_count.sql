-- how many of the supplied people the caller can actually see, using the same
-- rule media.get_persons applies.
--
-- exists so the clan functions share one definition of "a person you may put in
-- a clan" rather than each restating the join and the name/status guard.  a
-- caller compares the result against the number of distinct ids it asked about;
-- a shortfall means at least one id is not the caller's to use.
CREATE OR REPLACE FUNCTION media.get_visible_person_count
(
    _user_id UUID,
    _person_ids UUID[]
)
RETURNS INTEGER
AS $$
    SELECT COUNT(DISTINCT p.id)::INTEGER
    FROM media.person p
    INNER JOIN media.user_face uf
        ON uf.person_id = p.id
    WHERE
        uf.user_id = _user_id
        AND p.id = ANY(COALESCE(_person_ids, ARRAY[]::UUID[]))
        AND p.name IS NOT NULL
        AND p.status_code IS NULL;
$$ LANGUAGE sql STABLE;

GRANT EXECUTE
   ON FUNCTION media.get_visible_person_count
   TO maw_media;
