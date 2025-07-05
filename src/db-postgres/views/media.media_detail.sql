CREATE OR REPLACE VIEW media.media_detail AS
    SELECT
        m.id AS media_id,
        t.code AS media_type,
        fd.file_id,
        fd.file_path,
        fd.file_scale,
        fd.file_type
    FROM media.media m
    INNER JOIN media.type t
        ON t.id = m.type_id
    INNER JOIN media.file_detail fd
        ON fd.media_id = m.id;

GRANT SELECT
ON media.media_detail
TO maw_media;
