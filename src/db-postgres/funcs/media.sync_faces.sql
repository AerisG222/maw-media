-- applies a batch of detected faces published from maw-media-ai.
--
-- _payload is a bare json array.  must run after media.sync_persons, since
-- media.face.person_id is a foreign key.
--
-- maw-media-ai never learns this database's media ids.  a face carries the
-- published file path instead and it is resolved here against media.file, so a
-- file renamed upstream surfaces as an unresolved_path outcome rather than
-- silently linking to the wrong media.
--
-- outcomes:
--   applied         - inserted or updated
--   skipped_stale   - source_revision is not newer than what is already stored
--   unresolved_path - no media.file row matches the supplied path; detail is
--                     the path
--   unknown_person  - person_id has not been published yet; detail is that id.
--                     reported per face rather than raised as a foreign key
--                     violation, so one out-of-order row cannot sink a batch of
--                     a thousand
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

    RETURN QUERY
    WITH incoming AS (
        -- DISTINCT ON collapses an id repeated within one batch, keeping the
        -- highest revision
        SELECT DISTINCT ON (f.id) f.*
        FROM jsonb_to_recordset(coalesce(_payload, '[]'::jsonb))
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
            r.media_id,
            (i.person_id IS NULL OR pe.id IS NOT NULL) AS person_known
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
        LEFT OUTER JOIN media.person pe ON pe.id = i.person_id
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
            AND r.person_known
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
            WHEN NOT r.person_known THEN 'unknown_person'
            WHEN u.id IS NULL THEN 'skipped_stale'
            ELSE 'applied'
        END,
        CASE
            WHEN r.media_id IS NULL THEN r.file_path
            WHEN NOT r.person_known THEN r.person_id::TEXT
            ELSE NULL
        END
    FROM resolved r
    LEFT OUTER JOIN upserted u ON u.id = r.id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.sync_faces
    TO maw_media;
