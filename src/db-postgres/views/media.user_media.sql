CREATE OR REPLACE VIEW media.user_media AS
    SELECT DISTINCT
        cm.category_id,
        cm.media_id,
        uc.user_id
    FROM media.user_category uc
    INNER JOIN media.category_media cm
        ON uc.category_id = cm.category_id;

GRANT SELECT
ON media.user_media
TO maw_media;
