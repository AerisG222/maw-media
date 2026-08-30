-- maps the normalized reverse geocode text of one location level to the
-- media.place it resolves to.
--
-- this indirection is the entire reason media.place can be administered
-- independently of media.location.  media.assign_location_place never matches on
-- media.place.name - it matches here - so an admin who renames a place, or merges
-- two nodes the geocoder spelled differently, is not undone the next time
-- media.set_location_metadata runs over one of the affected coordinates.  without
-- it, every derivation pass would faithfully recreate the node that was just
-- merged away, and the merge would look like it silently failed.
--
-- every place gets an identity alias when it is created, so the ordinary path is
-- a plain lookup here rather than a fallback that only engages after an admin has
-- edited something.  that keeps the common case and the corrected case on one
-- code path.
--
-- the phase 0 audit found current data needs almost no merging - country and
-- state names are consistently long form and collide nowhere after
-- normalization, leaving only 'China / Guangdong' vs 'China / Guangdong Sheng'
-- and a 'Macao / Guangdong' mis-geocode.  so this is future-proofing.  it is
-- built now rather than later because it costs one insert at place creation,
-- whereas retrofitting it would mean migrating place ids that clients have
-- already bookmarked.
CREATE TABLE IF NOT EXISTS media.place_alias (
    place_id UUID NOT NULL,

    -- the level this text was read at, and the place it sits under.  the pair
    -- scopes the match: 'georgia' is a country under no parent and a state under
    -- the United States, and only the parent tells them apart.
    kind TEXT NOT NULL,
    parent_place_id UUID,

    -- media.normalize_place_name applied to the raw geocode value.  the raw value
    -- is deliberately not kept - media.location still holds it, and storing a
    -- second copy would invite the two drifting.
    match_key TEXT NOT NULL,

    created TIMESTAMPTZ NOT NULL,

    CONSTRAINT fk_media_place_alias$media_place
    FOREIGN KEY (place_id)
    REFERENCES media.place(id),

    CONSTRAINT fk_media_place_alias$media_place$parent
    FOREIGN KEY (parent_place_id)
    REFERENCES media.place(id),

    CONSTRAINT fk_media_place_alias$media_place_kind
    FOREIGN KEY (kind)
    REFERENCES media.place_kind(code)
);

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'place_alias'
            AND indexname = 'uq_media_place_alias$kind$parent_place_id$match_key'
    )
    THEN

        -- the lookup derivation performs, and the uniqueness that makes it
        -- deterministic: one (kind, parent, text) tuple resolves to exactly one
        -- place.  this stands in for a primary key.
        --
        -- NULLS NOT DISTINCT is load bearing rather than tidy.  parent_place_id is
        -- null for every country, and under the default rule nulls never compare
        -- equal - so a plain unique constraint would permit two rows for
        -- 'united states' at the country level, and derivation would pick between
        -- them arbitrarily.
        CREATE UNIQUE INDEX uq_media_place_alias$kind$parent_place_id$match_key
        ON media.place_alias(kind, parent_place_id, match_key)
        NULLS NOT DISTINCT;

    END IF;

    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'place_alias'
            AND indexname = 'ix_media_place_alias$place_id'
    )
    THEN

        -- the reverse direction: a merge repoints every alias of the losing place
        -- at the winner, and an admin screen shows which spellings feed a place
        CREATE INDEX ix_media_place_alias$place_id
        ON media.place_alias(place_id);

    END IF;
END
$$;

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.place_alias
TO maw_media;
