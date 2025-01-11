CREATE TABLE IF NOT EXISTS media.location (
    id UUID NOT NULL,
    latitude NUMERIC(8_6) NOT NULL,
    longitude NUMERIC(9_6) NOT NULL,
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
    PRIMARY KEY (id)
);

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.location
TO maw_api;
