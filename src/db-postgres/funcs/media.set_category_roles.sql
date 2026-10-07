-- sets the roles a category is granted to, replacing the ones it has.
--
-- the first writer of media.category_role this api has had - categories were
-- granted roles outside it until now.
--
-- all or nothing, like media.set_media_roles: everything is checked first, and if
-- anything is wrong nothing changes and every problem is returned.  one row per
-- problem, or a single 'applied' row:
--
--   not_admin            the caller is not an admin (alone)
--   not_found            no such category (alone)
--   no_roles             no roles were named (alone).  a category granted to
--                        nobody is hidden from everyone, admins included, with
--                        nothing in the api to find it again
--   unknown_role         detail names a role that does not exist
--   would_hide_from_you  the caller holds none of the new roles, so the change
--                        would hide the category from the admin making it.  for
--                        a media there is a listing to recover from that; for a
--                        category there is not
--   restriction_depends  affected_media_id is a restricted media in this
--                        category whose restriction lists detail, a role being
--                        removed.  removing it would leave that restriction
--                        naming a role the category no longer grants - exactly
--                        what media.set_media_roles refuses to create, since such
--                        a role grants nothing.  change the media's restriction
--                        first
--   applied              done
--
-- roles that stay are left untouched, so their created / created_by keep
-- recording when they were first granted.
--
-- the category's modified time is bumped, because clients sync categories
-- incrementally on it (media.get_categories, _modified_after): a user newly
-- granted the category would otherwise never receive it, its timestamp being
-- older than their last sync.  a user who *loses* the category simply stops
-- receiving it - a client that cached it keeps showing it until it resyncs, which
-- is a limit of incremental sync rather than something this can fix.
CREATE OR REPLACE FUNCTION media.set_category_roles
(
    _user_id UUID,
    _category_id UUID,
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

    IF NOT EXISTS (SELECT 1 FROM media.category c WHERE c.id = _category_id) THEN
        RETURN QUERY SELECT NULL::UUID, 'not_found'::TEXT, NULL::TEXT;
        RETURN;
    END IF;

    IF COALESCE(ARRAY_LENGTH(_role_names, 1), 0) = 0 THEN
        RETURN QUERY SELECT NULL::UUID, 'no_roles'::TEXT, NULL::TEXT;
        RETURN;
    END IF;

    RETURN QUERY
    WITH requested_roles AS
    (
        SELECT DISTINCT rn.name, r.id
        FROM UNNEST(_role_names) AS rn(name)
        LEFT OUTER JOIN media.role r
            ON r.name = rn.name
    ),
    removed_roles AS
    (
        SELECT cr.role_id, r.name
        FROM media.category_role cr
        INNER JOIN media.role r
            ON r.id = cr.role_id
        WHERE cr.category_id = _category_id
            AND cr.role_id NOT IN
            (
                SELECT rr.id
                FROM requested_roles rr
                WHERE rr.id IS NOT NULL
            )
    )
    SELECT NULL::UUID, 'unknown_role'::TEXT, rr.name
    FROM requested_roles rr
    WHERE rr.id IS NULL

    UNION ALL

    -- only judged once every role is known: with an unknown name in the list, the
    -- answer would be about a list that cannot be applied anyway
    SELECT NULL::UUID, 'would_hide_from_you'::TEXT, NULL::TEXT
    WHERE NOT EXISTS (SELECT 1 FROM requested_roles rr WHERE rr.id IS NULL)
        AND NOT EXISTS
        (
            SELECT 1
            FROM requested_roles rr
            INNER JOIN media.user_role ur
                ON ur.role_id = rr.id
            WHERE ur.user_id = _user_id
        )

    UNION ALL

    SELECT mr.media_id, 'restriction_depends'::TEXT, rem.name
    FROM removed_roles rem
    INNER JOIN media.media_role mr
        ON mr.role_id = rem.role_id
    INNER JOIN media.category_media cm
        ON cm.media_id = mr.media_id
        AND cm.category_id = _category_id;

    -- RETURN QUERY sets FOUND when it produced a row: any problem ends the call
    -- here, with every problem in the result and nothing changed
    IF FOUND THEN
        RETURN;
    END IF;

    DELETE FROM media.category_role cr
    WHERE cr.category_id = _category_id
        AND cr.role_id NOT IN
        (
            SELECT r.id
            FROM media.role r
            WHERE r.name = ANY(_role_names)
        );

    INSERT INTO media.category_role
    (
        category_id,
        role_id,
        created,
        created_by
    )
    SELECT
        _category_id,
        r.id,
        NOW(),
        _user_id
    FROM media.role r
    WHERE r.name = ANY(_role_names)
        AND NOT EXISTS
        (
            SELECT 1
            FROM media.category_role cr
            WHERE cr.category_id = _category_id
                AND cr.role_id = r.id
        );

    UPDATE media.category c
    SET modified = NOW(),
        modified_by = _user_id
    WHERE c.id = _category_id;

    RETURN QUERY SELECT NULL::UUID, 'applied'::TEXT, NULL::TEXT;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.set_category_roles
    TO maw_media;
