-- 2026-08-22 - add is_favorite, _favorites_only and _person_id
DROP FUNCTION IF EXISTS media.get_persons;

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
--
-- _person_id narrows the result to one person without changing any of the above,
-- so a caller holding an id gets the same shape and the same access rule rather
-- than a second function that could drift from this one.
CREATE OR REPLACE FUNCTION media.get_persons
(
    _user_id UUID,
    _favorites_only BOOLEAN = FALSE,
    _person_id UUID = NULL
)
RETURNS TABLE
(
    id UUID,
    name TEXT,
    slug TEXT,
    preferred_face_id UUID,
    media_count INTEGER,
    is_favorite BOOLEAN
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        p.id,
        p.name,
        p.slug,
        -- 2026-10-06 - only a preferred face this caller may see.
        --
        -- a person is listed because the caller can see *some* face of theirs,
        -- but preferred_face_id is a column on the person, published from
        -- maw-media-ai, and can name a face on any photo at all - including one
        -- in a category the caller has no role for, or one restricted away from
        -- every role they hold.  returning it anyway was a leak rather than a
        -- broken image: FaceRepository primes the face access cache with these
        -- ids, which would have let the caller fetch that crop for as long as
        -- the entry lived.
        --
        -- answered from rows this query already reads.  uf is the caller's
        -- visible faces of this person, so the preferred face is visible exactly
        -- when it is one of them.  a correlated EXISTS per person asked the same
        -- question and tripled the cost of the listing - each probe rebuilt the
        -- caller's category access from scratch, 540 times.
        --
        -- null rather than a substitute face, so the client falls back to its
        -- placeholder - the choice of face is maw-media-ai's, and inventing a
        -- different one here would show a face it never chose.
        CASE
            WHEN BOOL_OR(uf.face_id = p.preferred_face_id)
            THEN p.preferred_face_id
        END AS preferred_face_id,
        COUNT(DISTINCT uf.media_id)::INTEGER AS media_count,
        (pf.person_id IS NOT NULL) AS is_favorite
    FROM media.person p
    INNER JOIN media.user_face uf
        ON uf.person_id = p.id
    LEFT OUTER JOIN media.person_favorite pf
        ON pf.person_id = p.id
        AND pf.created_by = _user_id
    WHERE
        uf.user_id = _user_id
        -- unnamed clusters and triaged ones are not people to browse; the
        -- publisher excludes them, and this is the second line of defence
        AND p.name IS NOT NULL
        AND p.status_code IS NULL
        AND (_person_id IS NULL OR p.id = _person_id)
        AND (_favorites_only = FALSE OR pf.person_id IS NOT NULL)
    GROUP BY
        p.id,
        p.name,
        p.slug,
        p.preferred_face_id,
        pf.person_id
    ORDER BY
        -- favourites first: the picker's whole job is getting a caller to the
        -- handful of people they actually look for, and that beats any count
        (pf.person_id IS NOT NULL) DESC,
        COUNT(DISTINCT uf.media_id) DESC,
        p.name ASC;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_persons
    TO maw_media;
