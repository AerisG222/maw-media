CREATE TABLE IF NOT EXISTS media.location (
    id UUID NOT NULL,
    latitude NUMERIC(8, 6) NOT NULL,
    longitude NUMERIC(9, 6) NOT NULL,
    lookup_date TIMESTAMPTZ,
    formatted_address TEXT,
    administrative_area_level_1 TEXT,
    administrative_area_level_2 TEXT,
    administrative_area_level_3 TEXT,
    country TEXT,
    locality TEXT,
    neighborhood TEXT,
    sub_locality_level_1 TEXT,
    sub_locality_level_2 TEXT,
    postal_code TEXT,
    postal_code_suffix TEXT,
    premise TEXT,
    route TEXT,
    street_number TEXT,
    sub_premise TEXT,

    CONSTRAINT pk_media_location
    PRIMARY KEY (id),

    CONSTRAINT uq_media_location$latitude$longitude
    UNIQUE(latitude, longitude)
);

DO $$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE
            conname = 'uq_media_location$latitude$longitude'
            AND
            conrelid = 'media.location'::regclass
    )
    THEN
        ALTER TABLE media.location
            ADD CONSTRAINT uq_media_location$latitude$longitude
            UNIQUE(latitude, longitude);
    END IF;
END $$;

-- 2026-08-30 - begin - link a coordinate to the place a user browses it by
--
-- media.place is derived from the reverse geocode columns above, and this points
-- at the *deepest* node that could be resolved for them - the city where there is
-- one, otherwise the state, otherwise the country.  ancestors are read from the
-- parent chain rather than stored here, so a row cannot disagree with itself
-- about which state its city is in.
--
-- null means not browsable by location, which covers the 2,171 coordinates that
-- have never been geocoded as well as any future row whose geocode came back
-- without a country.  media.assign_location_place owns this column; nothing else
-- writes it.  see docs/browse-by-location.md
ALTER TABLE media.location
    ADD COLUMN IF NOT EXISTS place_id UUID;

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE
            conname = 'fk_media_location$media_place'
            AND
            conrelid = 'media.location'::regclass
    )
    THEN
        ALTER TABLE media.location
            ADD CONSTRAINT fk_media_location$media_place
            FOREIGN KEY (place_id)
            REFERENCES media.place(id);
    END IF;

    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'location'
            AND indexname = 'ix_media_location$place_id'
    )
    THEN

        -- every place read path arrives here: media.user_location joins location
        -- to find which coordinates belong to the places being browsed.  partial,
        -- because the 8% of rows still awaiting a geocode are never the answer.
        CREATE INDEX ix_media_location$place_id
        ON media.location(place_id)
        WHERE place_id IS NOT NULL;

    END IF;
END
$$;
-- 2026-08-30 - end - link a coordinate to the place a user browses it by

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.location
TO maw_media;
