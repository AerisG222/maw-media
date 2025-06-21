CREATE OR REPLACE VIEW media.media_detail AS
    SELECT
        m.id,
        t.code AS type,
        fd.path AS file_path,
        fd.scale AS file_scale,
        fd.type AS file_type
    FROM media.media m
    INNER JOIN media.type t
        ON t.id = m.type_id
    INNER JOIN media.file_detail fd
        ON fd.media_id = m.id;

GRANT SELECT
ON media.media_detail
TO maw_media;
