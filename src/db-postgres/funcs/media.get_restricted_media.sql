-- every restricted media, for an admin to find and manage - including the ones
-- the admin cannot see themselves.
--
-- the one place an admin learns which media are restricted.  the ordinary media
-- listings deliberately say nothing about it: a flag on every media would carry
-- an admin only concept to every caller - null for nearly all of them - and every
-- function returning media would have to remember to fill it in.  a client
-- wanting to badge or filter restricted photos fetches this beside the media,
-- and joins on media_id, the way the gps call works.
--
-- it is also the only way back to a photo restricted to roles you do not hold.
-- such a photo drops out of every listing you can make - media.user_media is the
-- rule for everybody, admins included - so this reads media.media_role directly
-- rather than through media.user_media, and says per row whether the caller can
-- see the media: is_visible_to_you.
--
-- metadata only, no files.  the caller may not be allowed to fetch the files of a
-- media hidden from them - /assets asks media.user_media too - and a listing that
-- handed out paths it would then refuse is worse than one that does not.  to see
-- such a photo, lift or widen its restriction.
--
-- no rows for a caller who is not an admin.  one row per (category, media), like
-- every other media listing, newest category first.
CREATE OR REPLACE FUNCTION media.get_restricted_media
(
    _user_id UUID,
    _category_id UUID = NULL
)
RETURNS TABLE
(
    media_id UUID,
    media_slug TEXT,
    media_type TEXT,
    category_id UUID,
    category_name TEXT,
    category_year SMALLINT,
    category_slug TEXT,
    roles TEXT[],
    is_visible_to_you BOOLEAN
)
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RETURN;
    END IF;

    -- every column is qualified: plpgsql makes each output column a variable in
    -- this body, and an unqualified media_id would be ambiguous
    RETURN QUERY
    SELECT
        cm.media_id,
        cm.slug,
        t.code,
        c.id,
        c.name,
        c.year,
        c.slug,
        ARRAY_AGG(DISTINCT r.name ORDER BY r.name),
        -- through media.user_media, so whether the caller can see it is decided by
        -- the one definition of that rule rather than a second copy here.  one row
        -- per (category, media, user), so the join cannot multiply the roles
        BOOL_OR(um.media_id IS NOT NULL)
    FROM media.media_role mr
    INNER JOIN media.role r
        ON r.id = mr.role_id
    INNER JOIN media.category_media cm
        ON cm.media_id = mr.media_id
    INNER JOIN media.category c
        ON c.id = cm.category_id
    INNER JOIN media.media m
        ON m.id = cm.media_id
    INNER JOIN media.type t
        ON t.id = m.type_id
    LEFT OUTER JOIN media.user_media um
        ON um.media_id = cm.media_id
        AND um.category_id = cm.category_id
        AND um.user_id = _user_id
    WHERE _category_id IS NULL
        OR cm.category_id = _category_id
    GROUP BY
        cm.media_id,
        cm.slug,
        t.code,
        c.id,
        c.name,
        c.year,
        c.slug,
        c.effective_date
    ORDER BY
        c.effective_date DESC,
        c.id,
        cm.slug;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_restricted_media
    TO maw_media;
