CREATE OR REPLACE VIEW media.user_category AS
    SELECT DISTINCT
        cr.category_id,
        ur.user_id
    FROM media.category_role cr
    INNER JOIN media.user_role ur
        ON cr.role_id = ur.role_id;

GRANT SELECT
ON media.user_category
TO maw_media;
