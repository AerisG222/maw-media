CREATE TABLE IF NOT EXISTS media.media (
    id UUID NOT NULL,
    type_id UUID NOT NULL,
    location_id UUID,
    location_override_id UUID,
    created TIMESTAMPTZ NOT NULL,
    created_by UUID NOT NULL,
    modified TIMESTAMPTZ NOT NULL,
    modified_by UUID NOT NULL,
    duration REAL,
    metadata JSONB,
    exif_latitude NUMERIC(8,6) GENERATED ALWAYS AS (
        CASE
            WHEN CAST(jsonb_path_query_first(metadata, '$.**."GPSLatitudeRef"."num"') #>> '{}' AS TEXT) = 'S'
                AND CAST(jsonb_path_query_first(metadata, '$.**."GPSLatitude"."num"') #>> '{}' AS NUMERIC(8,6)) > 0
            THEN -CAST(jsonb_path_query_first(metadata, '$.**."GPSLatitude"."num"') #>> '{}' AS NUMERIC(8,6))
            ELSE CAST(jsonb_path_query_first(metadata, '$.**."GPSLatitude"."num"') #>> '{}' AS NUMERIC(8,6))
        END
    ) STORED,
    exif_longitude NUMERIC(9,6) GENERATED ALWAYS AS (
        CASE
            WHEN CAST(jsonb_path_query_first(metadata, '$.**."GPSLongitudeRef"."num"') #>> '{}' AS TEXT) = 'W'
                AND CAST(jsonb_path_query_first(metadata, '$.**."GPSLongitude"."num"') #>> '{}' AS NUMERIC(9,6)) > 0
            THEN -CAST(jsonb_path_query_first(metadata, '$.**."GPSLongitude"."num"') #>> '{}' AS NUMERIC(9,6))
            ELSE CAST(jsonb_path_query_first(metadata, '$.**."GPSLongitude"."num"') #>> '{}' AS NUMERIC(9,6))
        END
    ) STORED,

    CONSTRAINT pk_media_media
    PRIMARY KEY (id),

    CONSTRAINT fk_media_media$media_type
    FOREIGN KEY (type_id)
    REFERENCES media.type(id),

    CONSTRAINT fk_media_media$media_location
    FOREIGN KEY (location_id)
    REFERENCES media.location(id),

    CONSTRAINT fk_media_media$media_location$override
    FOREIGN KEY (location_override_id)
    REFERENCES media.location(id),

    CONSTRAINT fk_media$media_user$created
    FOREIGN KEY (created_by)
    REFERENCES media.user(id),

    CONSTRAINT fk_media_media$media_user$modified
    FOREIGN KEY (modified_by)
    REFERENCES media.user(id)
);

-- 2026-07-04 - begin - materialize exif gps into stored columns so the
--   location correction worker no longer recomputes jsonb on every scan
ALTER TABLE media.media
    ADD COLUMN IF NOT EXISTS exif_latitude NUMERIC(8,6) GENERATED ALWAYS AS (
        CASE
            WHEN CAST(jsonb_path_query_first(metadata, '$.**."GPSLatitudeRef"."num"') #>> '{}' AS TEXT) = 'S'
                AND CAST(jsonb_path_query_first(metadata, '$.**."GPSLatitude"."num"') #>> '{}' AS NUMERIC(8,6)) > 0
            THEN -CAST(jsonb_path_query_first(metadata, '$.**."GPSLatitude"."num"') #>> '{}' AS NUMERIC(8,6))
            ELSE CAST(jsonb_path_query_first(metadata, '$.**."GPSLatitude"."num"') #>> '{}' AS NUMERIC(8,6))
        END
    ) STORED,
    ADD COLUMN IF NOT EXISTS exif_longitude NUMERIC(9,6) GENERATED ALWAYS AS (
        CASE
            WHEN CAST(jsonb_path_query_first(metadata, '$.**."GPSLongitudeRef"."num"') #>> '{}' AS TEXT) = 'W'
                AND CAST(jsonb_path_query_first(metadata, '$.**."GPSLongitude"."num"') #>> '{}' AS NUMERIC(9,6)) > 0
            THEN -CAST(jsonb_path_query_first(metadata, '$.**."GPSLongitude"."num"') #>> '{}' AS NUMERIC(9,6))
            ELSE CAST(jsonb_path_query_first(metadata, '$.**."GPSLongitude"."num"') #>> '{}' AS NUMERIC(9,6))
        END
    ) STORED;

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'media'
            AND indexname = 'ix_media_media$exif_gps'
    )
    THEN

        -- partial: only media that actually carry gps coordinates
        CREATE INDEX ix_media_media$exif_gps
        ON media.media(
            exif_latitude,
            exif_longitude
        )
        WHERE exif_latitude IS NOT NULL
            AND exif_longitude IS NOT NULL;

    END IF;
END
$$;
-- 2026-07-04 - end - materialize exif gps into stored columns

-- 2026-09-20 - begin - index the location a media is actually filed under
--
-- there are two location columns and neither was indexed, so nothing could reach
-- a media *from* its coordinate.  every place read therefore had to arrive from
-- the other end - walk all of the caller's media, resolve each one's location,
-- then discard the 99.9% that were somewhere else.  a city holding 70 photos cost
-- the same as the whole library.
--
-- the index is on the expression rather than on the two columns, because
-- media.media_location defines where a media *is* as
-- COALESCE(location_override_id, location_id) - the override wins, and it is the
-- majority path rather than the exception.  a plain index on either column alone
-- could not serve that expression, and the pair of them would still leave the
-- planner unable to answer the coalesce without rechecking every row.
--
-- partial for the same reason media.location$place_id is: 45% of the library has
-- no location at all, and those rows are never the answer to "what was taken
-- here".  the predicate matches media.media_location's own WHERE exactly, which
-- is what lets the planner use it for that view.
DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'media'
            AND indexname = 'ix_media_media$location'
    )
    THEN

        CREATE INDEX ix_media_media$location
        ON media.media(COALESCE(location_override_id, location_id))
        WHERE COALESCE(location_override_id, location_id) IS NOT NULL;

    END IF;
END
$$;
-- 2026-09-20 - end - index the location a media is actually filed under

GRANT INSERT, UPDATE, SELECT
ON media.media
TO maw_media;
