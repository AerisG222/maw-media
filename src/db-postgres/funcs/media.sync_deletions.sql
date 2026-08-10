-- removes persons or faces that maw-media-ai has dropped.
--
-- _payload is a bare json array of { entity_type, id }.  deletions are explicit
-- rather than inferred from absence: a batch is a delta, so a row missing from
-- a publish means "unchanged", not "gone".
--
-- these are hard deletes.  maw-media-ai is the system of record, so a row
-- removed there has no history worth preserving in a projection - a later
-- publish simply recreates it.  dropping a person leaves its faces in place but
-- unassigned, via ON DELETE SET NULL on media.face.person_id.
--
-- outcomes:
--   deleted        - removed
--   not_found      - named a row that is not here
--   unknown_entity - named something other than person or face
--   forbidden      - caller is not an admin
CREATE OR REPLACE FUNCTION media.sync_deletions
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
        SELECT DISTINCT ON (d.entity_type, d.id) d.*
        FROM jsonb_to_recordset(coalesce(_payload, '[]'::jsonb))
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
    ON FUNCTION media.sync_deletions
    TO maw_media;
