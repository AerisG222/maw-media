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

GRANT INSERT, UPDATE, SELECT
ON media.media
TO maw_media;
