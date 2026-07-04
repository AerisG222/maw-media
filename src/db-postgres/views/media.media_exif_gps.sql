DROP VIEW IF EXISTS media.media_exif_gps;

CREATE OR REPLACE VIEW media.media_exif_gps AS
    SELECT
        id AS media_id,
        location_id,
        location_override_id,
        exif_latitude,
        exif_longitude
    FROM media.media;

GRANT SELECT
    ON media.media_exif_gps
    TO maw_media;
