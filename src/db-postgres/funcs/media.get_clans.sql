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
    SELECT
        c.id AS clan_id,
        c.name AS clan_name,
        c.created,
        c.modified,
        p.id AS person_id,
        p.name AS person_name,
        p.slug AS person_slug,
        p.preferred_face_id,
        mc.media_count,
        (pf.person_id IS NOT NULL) AS is_favorite
    FROM media.clan c
    LEFT OUTER JOIN media.clan_person cp
        ON cp.clan_id = c.id
    LEFT OUTER JOIN media.person p
        ON p.id = cp.person_id
        AND p.name IS NOT NULL
        AND p.status_code IS NULL
    -- LATERAL rather than a join and GROUP BY: the count is per person and the
    -- rest of the row is not aggregated, so grouping would mean listing every
    -- clan and person column in a GROUP BY that has nothing to do with the intent
    LEFT OUTER JOIN LATERAL
    (
        SELECT COUNT(DISTINCT uf.media_id)::INTEGER AS media_count
        FROM media.user_face uf
        WHERE
            uf.user_id = _user_id
            AND uf.person_id = p.id
    ) mc ON TRUE
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
        mc.media_count DESC,
        p.name ASC;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_clans
    TO maw_media;
