-- removes persons that maw-media-ai has dropped.
--
-- _payload is a bare json array of uuids.  deletions are explicit rather than
-- inferred from absence: a batch is a delta, so a row missing from a publish
-- means "unchanged", not "gone".
--
-- these are hard deletes.  maw-media-ai is the system of record, so a row
-- removed there has no history worth preserving in a projection - a later
-- publish simply recreates it.  the faces stay put but become unassigned, via
-- ON DELETE SET NULL on media.face.person_id.
--
-- outcomes:
--   deleted   - removed
--   not_found - named a row that is not here
--   forbidden - caller is not an admin
CREATE OR REPLACE FUNCTION media.delete_persons
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
        SELECT DISTINCT elem::UUID AS id
        FROM jsonb_array_elements_text(coalesce(_payload, '[]'::jsonb)) AS elem
    ),
    removed AS (
        DELETE FROM media.person pe
        USING incoming i
        WHERE pe.id = i.id
        RETURNING pe.id
    )
    SELECT
        'person'::TEXT,
        i.id,
        CASE WHEN r.id IS NULL THEN 'not_found' ELSE 'deleted' END,
        NULL::TEXT
    FROM incoming i
    LEFT OUTER JOIN removed r ON r.id = i.id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.delete_persons
    TO maw_media;
