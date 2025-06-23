CREATE OR REPLACE VIEW media.media_gps AS
    SELECT
        m.id AS media_id,
        COALESCE
        (
            ol.latitude,
            sl.latitude
        ) AS latitude,
        COALESCE
        (
            ol.longitude,
            sl.longitude
        ) AS longitude
    FROM media.media m
    LEFT OUTER JOIN media.location sl
        ON m.location_id = sl.id
    LEFT OUTER JOIN media.location ol
        ON m.location_override_id = ol.id;

GRANT SELECT
ON media.media_gps
TO maw_media;
