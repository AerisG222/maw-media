CREATE OR REPLACE FUNCTION media.set_category_teaser
(
    _user_id UUID,
    _category_id UUID,
    _media_id UUID
)
RETURNS BIGINT
AS $$
DECLARE
    _rowcount BIGINT;
BEGIN

    -- currently not really necessary as the app will treat this as an admin only function
    IF NOT EXISTS (
        SELECT 1
        FROM media.user_category uc
        WHERE
            uc.user_id = _user_id
            AND uc.category_id = _category_id
    ) THEN
        RAISE 'not updating category teaser - user % does not have access to category %!', _user_id, _category_id;
    END IF;

    -- only update the teaser if the media belongs to the specified category
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
    ELSE
        RAISE 'not updating category teaser - media % does not belong to category %!', _media_id, _category_id;
    END IF;

    GET DIAGNOSTICS _rowcount = ROW_COUNT;

    RETURN _rowcount;

END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.set_category_teaser
    TO maw_media;
