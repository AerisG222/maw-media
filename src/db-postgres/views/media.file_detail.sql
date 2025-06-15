CREATE OR REPLACE VIEW media.file_detail AS
    SELECT
        f.media_id,
        f.path,
        s.code AS scale,
        t.code AS type
    FROM media.file f
    INNER JOIN media.scale s
        ON f.scale_id = s.id
    INNER JOIN media.type t
        ON f.type_id = t.id;

GRANT SELECT
ON media.file_detail
TO maw_media;
