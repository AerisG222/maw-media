-- a single detected face, published from maw-media-ai.
--
-- like media.person this is a projection.  the bounding box is stored
-- normalised (0..1) rather than in pixels so a crop can be rendered against any
-- scale in media.file without republishing when scales change.
--
-- as with media.person there are no created / modified audit columns: every row
-- is written by the same publisher service account, so they would record a
-- constant, and at face volumes that is two uuids and two timestamps per row to
-- store nothing.  `published` carries the only fact worth keeping.
--
-- no frame_time column yet.  video coverage starts with the poster frame only
-- (which resolves to the video's media_id and renders correctly against the
-- video's own scaled files).  sampling other frames would need a frame
-- reference to render a crop, and adding a nullable column then is an instant
-- ALTER TABLE plus an additive payload field.
CREATE TABLE IF NOT EXISTS media.face (
    id UUID NOT NULL,                    -- same uuid as maw-media-ai face_detection.id
    media_id UUID NOT NULL,              -- resolved from the published file path at sync time
    person_id UUID,                      -- null while the face is unassigned
    box_x NUMERIC(7,6) NOT NULL,         -- normalised 0..1; a detector may report slightly
    box_y NUMERIC(7,6) NOT NULL,         --   outside that range for a face cut off by the
    box_width NUMERIC(7,6) NOT NULL,     --   frame edge, so no range CHECK is applied
    box_height NUMERIC(7,6) NOT NULL,
    detection_score REAL NOT NULL,
    source_revision BIGINT NOT NULL,     -- monotonic revision from maw-media-ai
    published TIMESTAMPTZ NOT NULL,      -- when this row was last accepted from a publish
    deleted TIMESTAMPTZ,                 -- soft delete

    CONSTRAINT pk_media_face
    PRIMARY KEY (id),

    CONSTRAINT fk_media_face$media_media
    FOREIGN KEY (media_id)
    REFERENCES media.media(id),

    CONSTRAINT fk_media_face$media_person
    FOREIGN KEY (person_id)
    REFERENCES media.person(id)
);

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'face'
            AND indexname = 'ix_media_face$media_id'
    )
    THEN

        CREATE INDEX ix_media_face$media_id
        ON media.face(media_id);

    END IF;
END
$$;

DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_catalog.pg_indexes
        WHERE schemaname = 'media'
            AND tablename = 'face'
            AND indexname = 'ix_media_face$person_id'
    )
    THEN

        -- partial: "media containing this person" never looks at unassigned faces
        CREATE INDEX ix_media_face$person_id
        ON media.face(person_id)
        WHERE person_id IS NOT NULL
            AND deleted IS NULL;

    END IF;
END
$$;

-- media.person.preferred_face_id and media.face.person_id reference each other,
-- so this half of the cycle cannot be declared inline on media.person - that
-- table is created first.  added here, once both tables exist.
DO
$$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE
            conname = 'fk_media_person$media_face$preferred'
            AND
            conrelid = 'media.person'::regclass
    )
    THEN
        ALTER TABLE media.person
            ADD CONSTRAINT fk_media_person$media_face$preferred
            FOREIGN KEY (preferred_face_id)
            REFERENCES media.face(id);
    END IF;
END
$$;

GRANT SELECT, INSERT, UPDATE
ON media.face
TO maw_media;
