-- a caller's clans and their members.
--
-- returns one row per (clan, member), and one row with a null person id for a
-- clan with no visible members - the repository groups them back up.  members
-- come back as the same shape media.get_persons returns, so a client can render
-- a clan with face thumbnails without a second call.
--
-- members are filtered through media.user_face exactly as the person list is: a
-- clan is a saved selection, and access can be revoked after it was saved, so
-- membership is never allowed to be the thing that reveals a person.
--
-- _clan_id narrows to one clan without changing any of the above, so a caller
-- holding an id gets the same shape and the same ownership rule.
CREATE OR REPLACE FUNCTION media.get_clans
(
    _user_id UUID,
    _clan_id UUID = NULL
)
RETURNS TABLE
(
    clan_id UUID,
    clan_name TEXT,
    created TIMESTAMPTZ,
    modified TIMESTAMPTZ,
    person_id UUID,
    person_name TEXT,
    person_slug TEXT,
    preferred_face_id UUID,
    media_count INTEGER,
    is_favorite BOOLEAN
)
AS $$
BEGIN
    RETURN QUERY
    WITH members AS
    (
        -- the distinct people across the clans this call will return.  the count
        -- below is per person, but a person can be in several clans, and the
        -- LATERAL this replaces recomputed their count once per membership.
        --
        -- deliberately not repeating the named / untriaged filter applied to
        -- media.person further down: an extra count for somebody who is then
        -- dropped costs a little, and stating that rule twice risks the two
        -- copies disagreeing later.
        SELECT DISTINCT cp.person_id
        FROM media.clan c
        INNER JOIN media.clan_person cp
            ON cp.clan_id = c.id
        WHERE
            c.created_by = _user_id
            AND (_clan_id IS NULL OR c.id = _clan_id)
    ),
    counts AS MATERIALIZED
    (
        -- one grouped pass, which is what makes this affordable.
        -- media.user_face is a DISTINCT over the whole permission chain, so
        -- every visit to it is expensive and the point is to make exactly one -
        -- MATERIALIZED says so outright, and stops a later planner decision from
        -- quietly turning this back into a per row lookup.
        SELECT
            uf.person_id,
            COUNT(DISTINCT uf.media_id)::INTEGER AS media_count
        FROM media.user_face uf
        INNER JOIN members m
            ON m.person_id = uf.person_id
        WHERE uf.user_id = _user_id
        GROUP BY uf.person_id
    )
    SELECT
        c.id AS clan_id,
        c.name AS clan_name,
        c.created,
        c.modified,
        p.id AS person_id,
        p.name AS person_name,
        p.slug AS person_slug,
        p.preferred_face_id,
        -- a member with no visible media has no row in counts, where the
        -- LATERAL - an aggregate with no GROUP BY - always produced one holding
        -- a zero.  coalescing keeps the column exactly as it was
        COALESCE(mc.media_count, 0)::INTEGER AS media_count,
        (pf.person_id IS NOT NULL) AS is_favorite
    FROM media.clan c
    LEFT OUTER JOIN media.clan_person cp
        ON cp.clan_id = c.id
    LEFT OUTER JOIN media.person p
        ON p.id = cp.person_id
        AND p.name IS NOT NULL
        AND p.status_code IS NULL
    LEFT OUTER JOIN counts mc
        ON mc.person_id = p.id
    LEFT OUTER JOIN media.person_favorite pf
        ON pf.person_id = p.id
        AND pf.created_by = _user_id
    WHERE
        c.created_by = _user_id
        AND (_clan_id IS NULL OR c.id = _clan_id)
        -- a member the caller can no longer see drops out, but the clan itself
        -- stays: it is theirs, and an empty clan is a real state
        AND (p.id IS NULL OR COALESCE(mc.media_count, 0) > 0)
    ORDER BY
        c.name ASC,
        (pf.person_id IS NOT NULL) DESC,
        COALESCE(mc.media_count, 0) DESC,
        p.name ASC;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_clans
    TO maw_media;
