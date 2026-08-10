-- applies a batch of persons published from maw-media-ai.
--
-- _payload is a bare json array.  an upsert replaces the whole row, so the
-- publisher must send every field on every publish; omitting one clears it.
--
-- must run after media.sync_person_statuses (status_code is a foreign key) and
-- before media.sync_faces (media.face.person_id is a foreign key to this).
--
-- outcomes:
--   applied       - inserted or updated
--   skipped_stale - source_revision is not newer than what is already stored,
--                   which is what makes retries and out-of-order batches safe
--   forbidden     - caller is not an admin
CREATE OR REPLACE FUNCTION media.sync_persons
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

    RETURN QUERY
    WITH incoming AS (
        -- DISTINCT ON collapses an id repeated within one batch, keeping the
        -- highest revision
        SELECT DISTINCT ON (p.id) p.*
        FROM jsonb_to_recordset(coalesce(_payload, '[]'::jsonb))
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
            -- no foreign key: the named face is normally published after this
            -- call, so it is stored as a hint and resolved at read time
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
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.sync_persons
    TO maw_media;
