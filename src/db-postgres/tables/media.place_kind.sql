-- the levels of the place hierarchy that media.place forms.
--
-- a lookup table rather than a CHECK, for the reason media.person_status gives -
-- a new level costs an upsert rather than a migration, and the api can expose the
-- valid values - and for one more that this table adds: `level` makes the
-- country/state/city ordering *data* rather than a CASE expression repeated in
-- every function that walks the tree.  media.assign_location_place, the
-- parent/child validity rule, breadcrumb assembly in media.get_places and the
-- deferred media.set_place_parent all need that ordering, and four copies of it
-- would drift apart.
--
-- the other lookups here carry payload too - media.scale has width and height,
-- media.person_status has label and sort_order - so a bare list of codes would be
-- the odd one out rather than the norm.
--
-- `code` is the primary key rather than a uuid surrogate, following
-- media.person_status rather than media.type and media.scale.  those two force a
-- join on every read that wants the code - media.get_categories joins media.type
-- for nothing but xt.code - whereas referencing by code keeps media.place.kind
-- human readable and leaves the join to the functions that actually want `level`.
--
-- seeded rather than synced, unlike media.person_status: nothing publishes these
-- codes, we own them.  see seed/media.place_kind.sql.
CREATE TABLE IF NOT EXISTS media.place_kind (
    code TEXT NOT NULL,
    label TEXT NOT NULL,
    level SMALLINT NOT NULL,

    CONSTRAINT pk_media_place_kind
    PRIMARY KEY (code),

    -- one kind per level.  the level is what orders the hierarchy, so two kinds
    -- sharing one would make "does this place sit below that one" unanswerable.
    CONSTRAINT uq_media_place_kind$level
    UNIQUE (level)
);

-- read only for the application: these rows arrive with the deploy, and nothing
-- at runtime has cause to add a level
GRANT SELECT
ON media.place_kind
TO maw_media;
