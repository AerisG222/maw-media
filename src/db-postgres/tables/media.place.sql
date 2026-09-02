-- a browsable place: a country, a state, or a city.
--
-- derived from the reverse geocode columns on media.location, but deliberately
-- not owned by them.  media.location is one row per unique coordinate, so a
-- single city is smeared across hundreds of rows with no id to click on and no
-- place to hang a correction.  this is the entity a user actually browses, and
-- the one an admin can rename, merge or hide without touching a coordinate.
--
-- the tree is shallow and small - the phase 0 audit measured 8 countries, 31
-- states and 239 cities across 27,870 locations - so ancestry is walked with a
-- recursive CTE in media.get_place_descendants rather than kept in a closure
-- table.
--
-- `name` and `slug` are display concerns and are admin editable.  what a
-- location actually *matches* on lives in media.place_alias, and that separation
-- is what stops a rename from being quietly undone by the next geocode.
--
-- two invariants cannot be expressed as CHECK constraints here, because both
-- span rows:
--
--   * a parent must sit at a lower media.place_kind.level than its child, which
--     is what keeps a city from ending up beneath another city
--   * only a country may have a null parent_id
--
-- both are enforced in media.assign_location_place and the deferred
-- media.set_place_parent.  a trigger would catch them closer to the data, but
-- this schema has no triggers anywhere and introducing the first one for an
-- invariant that only two functions can violate is a poor trade.
--
-- this table is continued in tables/media.place_2.sql, which adds the cover image
-- columns.  they cannot be declared here: their foreign keys point at media.media
-- and media.user, and the schema's keys form a cycle - media.location references
-- media.place, and media.media references media.location - so media.place has to
-- be created before either of them exists.
--
-- see docs/browse-by-location.md
CREATE TABLE IF NOT EXISTS media.place (
    id UUID NOT NULL,
    parent_id UUID,                                 -- null only for countries
    kind TEXT NOT NULL,
    name TEXT NOT NULL,
    slug TEXT NOT NULL,

    -- lets an admin retire a node - a mis-geocode, or the losing side of a merge
    -- - without deleting a row that media.location and media.place_alias still
    -- reference.  read paths filter on it; derivation ignores it, so a hidden
    -- place stays the stable target of its aliases.
    is_hidden BOOLEAN NOT NULL DEFAULT FALSE,

    created TIMESTAMPTZ NOT NULL,
    modified TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_media_place
    PRIMARY KEY (id),

    CONSTRAINT fk_media_place$media_place$parent
    FOREIGN KEY (parent_id)
    REFERENCES media.place(id),

    CONSTRAINT fk_media_place$media_place_kind
    FOREIGN KEY (kind)
    REFERENCES media.place_kind(code),

    CONSTRAINT ck_media_place$name
    CHECK (LENGTH(TRIM(name)) > 0)
);

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'place'
            AND indexname = 'ix_media_place$parent_id$kind'
    )
    THEN

        -- the drill-down: every browse request asks for the children of one
        -- parent, and the root listing asks for parent_id IS NULL.  btree indexes
        -- nulls, so the same index serves both.
        CREATE INDEX ix_media_place$parent_id$kind
        ON media.place(parent_id, kind);

    END IF;

    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'place'
            AND indexname = 'uq_media_place$parent_id$slug'
    )
    THEN

        -- slugs are unique per parent, not globally: /united-states/new-york and
        -- /australia/new-york are different places and both are correct.
        --
        -- NULLS NOT DISTINCT is what makes this hold for countries.  parent_id is
        -- null for all of them, and under the default nulls-are-distinct rule two
        -- countries could both take the slug 'france' without colliding.
        CREATE UNIQUE INDEX uq_media_place$parent_id$slug
        ON media.place(parent_id, slug)
        NULLS NOT DISTINCT;

    END IF;
END
$$;

GRANT SELECT, INSERT, UPDATE, DELETE
ON media.place
TO maw_media;
