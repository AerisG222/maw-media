CREATE OR REPLACE VIEW media.media_gps AS
    SELECT
        m.id AS media_id,
        rl.latitude AS recorded_latitude,
        rl.longitude AS recorded_longitude,
        ol.latitude AS override_latitude,
        ol.longitude AS override_longitude
    FROM media.media m
    LEFT OUTER JOIN media.location rl
        ON m.location_id = rl.id
    LEFT OUTER JOIN media.location ol
        ON m.location_override_id = ol.id;

GRANT SELECT
ON media.media_gps
TO maw_media;
