CREATE OR REPLACE VIEW media.media_exif_gps AS
    WITH gps
    AS
    (
        SELECT
            m.id,
            m.location_id,
            m.location_override_id,
            CAST(jsonb_path_query_first(m.metadata, '$.**."GPSLatitude"."num"') #>> '{}' AS NUMERIC(8,6)) AS lat,
            CAST(jsonb_path_query_first(m.metadata, '$.**."GPSLatitudeRef"."num"') #>> '{}' AS TEXT) AS latref,
            CAST(jsonb_path_query_first(m.metadata, '$.**."GPSLongitude"."num"') #>> '{}' AS NUMERIC(9,6)) AS lng,
            CAST(jsonb_path_query_first(m.metadata, '$.**."GPSLongitudeRef"."num"') #>> '{}' AS TEXT) AS lngref
        FROM media.media m
    )
    SELECT
        id AS media_id,
        location_id,
        location_override_id,
        CASE WHEN latref = 'S' AND lat > 0
            THEN -lat ELSE lat END AS exif_latitude,
        CASE WHEN lngref = 'W' AND lng > 0
            THEN -lng ELSE lng END AS exif_longitude
    FROM gps;

GRANT SELECT
    ON media.media_exif_gps
    TO maw_media;
