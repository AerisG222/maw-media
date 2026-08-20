-- the named people a user may see, for the person picker.
--
-- returns everything rather than a page: the set is small (a few hundred) and
-- clients filter it locally, which beats a round trip per keystroke.
--
-- media_count is computed per user through media.user_face.  the published
-- person.face_count is the *global* figure from maw-media-ai and is never
-- exposed - it would tell a restricted caller how many photos exist that they
-- cannot see.
--
-- a person with no visible media does not appear at all, since the join is an
-- inner one.  that is the whole access rule: if you cannot see a single photo
-- of someone, you do not learn they exist.
CREATE OR REPLACE FUNCTION media.get_persons
(
    _user_id UUID
)
RETURNS TABLE
(
    id UUID,
    name TEXT,
    slug TEXT,
    preferred_face_id UUID,
    media_count INTEGER
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        p.id,
        p.name,
        p.slug,
        p.preferred_face_id,
        COUNT(DISTINCT uf.media_id)::INTEGER AS media_count
    FROM media.person p
    INNER JOIN media.user_face uf
        ON uf.person_id = p.id
    WHERE
        uf.user_id = _user_id
        -- unnamed clusters and triaged ones are not people to browse; the
        -- publisher excludes them, and this is the second line of defence
        AND p.name IS NOT NULL
        AND p.status_code IS NULL
    GROUP BY
        p.id,
        p.name,
        p.slug,
        p.preferred_face_id
    ORDER BY
        COUNT(DISTINCT uf.media_id) DESC,
        p.name ASC;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_persons
    TO maw_media;
