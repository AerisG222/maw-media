CREATE OR REPLACE FUNCTION media.set_category_teaser
(
    _user_id UUID,
    _category_id UUID,
    _media_id UUID
)
RETURNS INTEGER
AS $$
BEGIN

    -- only category owners can update the teaser
    IF NOT EXISTS (
        SELECT 1
        FROM media.user_category uc
        INNER JOIN media.category c
            ON c.id = uc.category_id
        WHERE
            uc.user_id = _user_id
            AND uc.category_id = _category_id
            AND c.created_by = _user_id
    ) THEN
        RAISE NOTICE 'not updating category teaser - user % does not have access and/or owns category %!', _user_id, _category_id;
        RETURN 1;
    END IF;

    -- only update the teaser if the media belongs to the specified category
    -- 2026-10-06 - a restricted media cannot be a teaser.  the teaser is the
    -- category's tile, shown to everyone who can see the category - which is
    -- exactly the audience a restriction exists to narrow.  refused rather than
    -- silently allowed, so the caller learns why, and media.set_media_roles
    -- refuses the reverse: restricting a media that is already a teaser.
    IF EXISTS (
        SELECT 1
        FROM media.media_role mr
        WHERE mr.media_id = _media_id
    ) THEN
        RAISE NOTICE 'not updating category teaser - media % is restricted', _media_id;
        RETURN 3;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM media.category_media cm
        WHERE
            cm.category_id = _category_id
            AND cm.media_id = _media_id
    ) THEN
        -- clear existing teaser
        UPDATE media.category_media cm
        SET is_teaser = false
        WHERE
            cm.category_id = _category_id
            AND cm.is_teaser = true;

        -- set the teaser for the specified media in the specified category
        UPDATE media.category_media cm
        SET is_teaser = true
        WHERE
            cm.category_id = _category_id
            AND cm.media_id = _media_id;

        UPDATE media.category
            SET
                modified_by = _user_id,
                modified = NOW()
            WHERE
                id = _category_id;
    ELSE
        RAISE NOTICE 'not updating category teaser - media % does not belong to category %!', _media_id, _category_id;
        RETURN 2;
    END IF;

    RETURN 0;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.set_category_teaser
    TO maw_media;
