-- restricts one or more media to the named roles, replacing any restriction they
-- already carry.  see tables/media.media_role.sql for what a restriction means.
--
-- all or nothing.  every media in the request is checked first, and if any of
-- them cannot be restricted, nothing changes and every problem found is returned
-- - not just the first.  a bulk restriction is a selection from an admin screen,
-- and applying the half that passed would leave a state nobody chose, then make
-- them work out which half that was.
--
-- one row per problem, or one 'applied' row per media when it succeeds:
--
--   not_admin         the caller is not an admin (alone, with no media id)
--   no_media          no media were named (alone)
--   no_roles          no roles were named (alone).  an empty list would mean
--                     "visible to nobody", which is never what an empty picker
--                     meant - removing a restriction is media.clear_media_roles
--   unknown_role      detail names a role that does not exist (no media id)
--   not_found         no such media
--   role_not_granted  detail names a role that no category holding the media
--                     grants.  a restriction narrows a category and cannot widen
--                     it, so that role could never see the photo anyway - and a
--                     list made only of such roles would hide it from everyone.
--                     refused so the admin finds out now rather than when the
--                     photo goes missing
--   teaser            detail names the category whose tile this media is.  the
--                     teaser is shown to everyone who can see the category, so
--                     change it first - media.set_category_teaser refuses the
--                     reverse
--   place_cover       detail names the place whose cover this media is.  covers
--                     are served to every signed in caller without a per file
--                     check, so clear or change it first
--   applied           the restriction is in place
--
-- the output column is affected_media_id rather than media_id: a plpgsql output
-- column is also a variable in the body, and one named like a table column would
-- make every reference to that column ambiguous.
CREATE OR REPLACE FUNCTION media.set_media_roles
(
    _user_id UUID,
    _media_ids UUID[],
    _role_names TEXT[]
)
RETURNS TABLE
(
    affected_media_id UUID,
    outcome TEXT,
    detail TEXT
)
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RETURN QUERY SELECT NULL::UUID, 'not_admin'::TEXT, NULL::TEXT;
        RETURN;
    END IF;

    IF COALESCE(ARRAY_LENGTH(_media_ids, 1), 0) = 0 THEN
        RETURN QUERY SELECT NULL::UUID, 'no_media'::TEXT, NULL::TEXT;
        RETURN;
    END IF;

    IF COALESCE(ARRAY_LENGTH(_role_names, 1), 0) = 0 THEN
        RETURN QUERY SELECT NULL::UUID, 'no_roles'::TEXT, NULL::TEXT;
        RETURN;
    END IF;

    RETURN QUERY
    WITH requested_media AS
    (
        SELECT DISTINCT m.id
        FROM UNNEST(_media_ids) AS m(id)
    ),
    requested_roles AS
    (
        SELECT DISTINCT rn.name, r.id
        FROM UNNEST(_role_names) AS rn(name)
        LEFT OUTER JOIN media.role r
            ON r.name = rn.name
    )
    SELECT NULL::UUID, 'unknown_role'::TEXT, rr.name
    FROM requested_roles rr
    WHERE rr.id IS NULL

    UNION ALL

    SELECT rm.id, 'not_found'::TEXT, NULL::TEXT
    FROM requested_media rm
    WHERE NOT EXISTS (SELECT 1 FROM media.media mm WHERE mm.id = rm.id)

    UNION ALL

    SELECT rm.id, 'role_not_granted'::TEXT, rr.name
    FROM requested_media rm
    CROSS JOIN requested_roles rr
    WHERE rr.id IS NOT NULL
        AND EXISTS (SELECT 1 FROM media.media mm WHERE mm.id = rm.id)
        AND NOT EXISTS
        (
            SELECT 1
            FROM media.category_media cm
            INNER JOIN media.category_role cr
                ON cr.category_id = cm.category_id
            WHERE cm.media_id = rm.id
                AND cr.role_id = rr.id
        )

    UNION ALL

    SELECT rm.id, 'teaser'::TEXT, c.name
    FROM requested_media rm
    INNER JOIN media.category_media cm
        ON cm.media_id = rm.id
        AND cm.is_teaser
    INNER JOIN media.category c
        ON c.id = cm.category_id

    UNION ALL

    SELECT rm.id, 'place_cover'::TEXT, p.name
    FROM requested_media rm
    INNER JOIN media.place p
        ON p.cover_media_id = rm.id;

    -- RETURN QUERY sets FOUND when it produced a row, so any problem at all ends
    -- the call here, with every problem already in the result and nothing changed
    IF FOUND THEN
        RETURN;
    END IF;

    DELETE FROM media.media_role mr
    WHERE mr.media_id = ANY(_media_ids);

    INSERT INTO media.media_role
    (
        media_id,
        role_id,
        created,
        created_by
    )
    SELECT DISTINCT
        m.id,
        r.id,
        NOW(),
        _user_id
    FROM UNNEST(_media_ids) AS m(id)
    INNER JOIN media.role r
        ON r.name = ANY(_role_names);

    RETURN QUERY
    SELECT DISTINCT m.id, 'applied'::TEXT, NULL::TEXT
    FROM UNNEST(_media_ids) AS m(id);
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.set_media_roles
    TO maw_media;
