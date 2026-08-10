-- applies one publish batch from maw-media-ai.
--
-- the whole batch is a single statement sequence inside the caller's
-- transaction, applied in dependency order: statuses, then persons, then faces,
-- then deletions.  media.person.preferred_face_id is DEFERRABLE INITIALLY
-- DEFERRED so a person may name a face that appears later in the same batch.
--
-- maw-media-ai never learns this database's media ids.  a face carries the
-- published file path instead and it is resolved here against media.file, so a
-- rename upstream surfaces as an unresolved_path outcome rather than silently
-- linking to the wrong media.
--
-- every row reports an outcome so the publisher can act on partial success:
--   applied         - inserted or updated
--   skipped_stale   - source_revision is not newer than what is already stored,
--                     which is what makes retries and out-of-order batches safe
--   unresolved_path - no media.file row matches the supplied path
--   deleted         - removed
--   not_found       - a deletion named a row that is not here
--   unknown_entity  - a deletion named something other than person or face
--   forbidden       - caller is not an admin
CREATE OR REPLACE FUNCTION media.sync_faces
(
    _user_id UUID,
    _payload JSONB
)
RETURNS TABLE
(
    entity TEXT,
    entity_id UUID,
    outcome TEXT,
    detail TEXT
)
AS $$
BEGIN
    IF NOT (SELECT * FROM media.get_is_admin(_user_id)) THEN
        RETURN QUERY SELECT 'batch'::TEXT, NULL::UUID, 'forbidden'::TEXT, NULL::TEXT;

        RETURN;
    END IF;

    -- statuses first: media.person.status_code is a foreign key to these, and
    -- the table is deliberately not seeded (maw-media-ai owns the codes)
    RETURN QUERY
    WITH incoming AS (
        SELECT DISTINCT ON (s.code) s.*
        FROM jsonb_to_recordset(coalesce(_payload->'statuses', '[]'::jsonb))
            AS s(
                code TEXT,
                label TEXT,
                description TEXT,
                sort_order INTEGER
            )
        ORDER BY s.code
    ),
    upserted AS (
        INSERT INTO media.person_status AS ps (code, label, description, sort_order)
        SELECT i.code, i.label, i.description, coalesce(i.sort_order, 0)
        FROM incoming i
        ON CONFLICT (code) DO UPDATE
            SET label = EXCLUDED.label,
                description = EXCLUDED.description,
                sort_order = EXCLUDED.sort_order
        RETURNING ps.code
    )
    SELECT
        'status'::TEXT,
        NULL::UUID,
        'applied'::TEXT,
        i.code
    FROM incoming i
    INNER JOIN upserted u ON u.code = i.code;

    -- DISTINCT ON collapses a duplicated id within one batch, keeping the
    -- highest revision.  without it postgres raises "ON CONFLICT DO UPDATE
    -- command cannot affect row a second time", which is a confusing way to
    -- learn the payload repeated itself.
    RETURN QUERY
    WITH incoming AS (
        SELECT DISTINCT ON (p.id) p.*
        FROM jsonb_to_recordset(coalesce(_payload->'persons', '[]'::jsonb))
            AS p(
                id UUID,
                name TEXT,
                slug TEXT,
                status_code TEXT,
                preferred_face_id UUID,
                face_count INTEGER,
                source_revision BIGINT,
                source_modified TIMESTAMPTZ
            )
        ORDER BY p.id, p.source_revision DESC
    ),
    upserted AS (
        INSERT INTO media.person AS pe (
            id,
            name,
            slug,
            status_code,
            preferred_face_id,
            face_count,
            source_revision,
            source_modified,
            published
        )
        SELECT
            i.id,
            i.name,
            i.slug,
            i.status_code,
            i.preferred_face_id,
            coalesce(i.face_count, 0),
            i.source_revision,
            i.source_modified,
            NOW()
        FROM incoming i
        ON CONFLICT (id) DO UPDATE
            SET name = EXCLUDED.name,
                slug = EXCLUDED.slug,
                status_code = EXCLUDED.status_code,
                preferred_face_id = EXCLUDED.preferred_face_id,
                face_count = EXCLUDED.face_count,
                source_revision = EXCLUDED.source_revision,
                source_modified = EXCLUDED.source_modified,
                published = EXCLUDED.published
            WHERE pe.source_revision < EXCLUDED.source_revision
        RETURNING pe.id
    )
    SELECT
        'person'::TEXT,
        i.id,
        CASE WHEN u.id IS NULL THEN 'skipped_stale' ELSE 'applied' END,
        NULL::TEXT
    FROM incoming i
    LEFT OUTER JOIN upserted u ON u.id = i.id;

    RETURN QUERY
    WITH incoming AS (
        SELECT DISTINCT ON (f.id) f.*
        FROM jsonb_to_recordset(coalesce(_payload->'faces', '[]'::jsonb))
            AS f(
                id UUID,
                file_path TEXT,
                person_id UUID,
                box_x NUMERIC(7,6),
                box_y NUMERIC(7,6),
                box_width NUMERIC(7,6),
                box_height NUMERIC(7,6),
                detection_score REAL,
                source_revision BIGINT
            )
        ORDER BY f.id, f.source_revision DESC
    ),
    resolved AS (
        SELECT
            i.*,
            r.media_id
        FROM incoming i
        LEFT JOIN LATERAL (
            -- media.file.path has no unique constraint, so pick deterministically
            -- rather than fanning one face out into several rows
            SELECT mf.media_id
            FROM media.file mf
            WHERE mf.path = i.file_path
            ORDER BY mf.id
            LIMIT 1
        ) r ON TRUE
    ),
    upserted AS (
        INSERT INTO media.face AS fa (
            id,
            media_id,
            person_id,
            box_x,
            box_y,
            box_width,
            box_height,
            detection_score,
            source_revision,
            published
        )
        SELECT
            r.id,
            r.media_id,
            r.person_id,
            r.box_x,
            r.box_y,
            r.box_width,
            r.box_height,
            r.detection_score,
            r.source_revision,
            NOW()
        FROM resolved r
        WHERE r.media_id IS NOT NULL
        ON CONFLICT (id) DO UPDATE
            SET media_id = EXCLUDED.media_id,
                person_id = EXCLUDED.person_id,
                box_x = EXCLUDED.box_x,
                box_y = EXCLUDED.box_y,
                box_width = EXCLUDED.box_width,
                box_height = EXCLUDED.box_height,
                detection_score = EXCLUDED.detection_score,
                source_revision = EXCLUDED.source_revision,
                published = EXCLUDED.published
            WHERE fa.source_revision < EXCLUDED.source_revision
        RETURNING fa.id
    )
    SELECT
        'face'::TEXT,
        r.id,
        CASE
            WHEN r.media_id IS NULL THEN 'unresolved_path'
            WHEN u.id IS NULL THEN 'skipped_stale'
            ELSE 'applied'
        END,
        CASE WHEN r.media_id IS NULL THEN r.file_path ELSE NULL END
    FROM resolved r
    LEFT OUTER JOIN upserted u ON u.id = r.id;

    -- deletions are explicit rather than inferred from absence: a batch is a
    -- delta, so a row missing from it means "unchanged", not "gone".
    --
    -- these are hard deletes.  maw-media-ai is the system of record, so a row
    -- removed there has no history worth preserving here; a later publish simply
    -- recreates it.  dropping a person leaves its faces in place but unassigned,
    -- via ON DELETE SET NULL on media.face.person_id.
    RETURN QUERY
    WITH incoming AS (
        SELECT DISTINCT ON (d.entity_type, d.id) d.*
        FROM jsonb_to_recordset(coalesce(_payload->'deletions', '[]'::jsonb))
            AS d(
                entity_type TEXT,
                id UUID
            )
        ORDER BY d.entity_type, d.id
    ),
    deleted_face AS (
        DELETE FROM media.face fa
        USING incoming i
        WHERE i.entity_type = 'face'
            AND fa.id = i.id
        RETURNING fa.id
    ),
    deleted_person AS (
        DELETE FROM media.person pe
        USING incoming i
        WHERE i.entity_type = 'person'
            AND pe.id = i.id
        RETURNING pe.id
    )
    SELECT
        i.entity_type,
        i.id,
        CASE
            WHEN i.entity_type NOT IN ('person', 'face') THEN 'unknown_entity'
            WHEN dp.id IS NOT NULL OR df.id IS NOT NULL THEN 'deleted'
            ELSE 'not_found'
        END,
        NULL::TEXT
    FROM incoming i
    LEFT OUTER JOIN deleted_person dp ON dp.id = i.id AND i.entity_type = 'person'
    LEFT OUTER JOIN deleted_face df ON df.id = i.id AND i.entity_type = 'face';
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.sync_faces
    TO maw_media;
