-- applies a batch of person statuses published from maw-media-ai.
--
-- _payload is a bare json array.  media.person_status is deliberately not
-- seeded here (maw-media-ai owns the codes), and media.person.status_code is a
-- foreign key to it, so a publisher must call this before media.sync_persons.
--
-- outcomes: applied, forbidden.  detail carries the code.
CREATE OR REPLACE FUNCTION media.sync_person_statuses
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
        -- DISTINCT ON collapses a code repeated within one batch.  without it
        -- postgres raises "ON CONFLICT DO UPDATE command cannot affect row a
        -- second time", which is a confusing way to learn the payload repeated
        -- itself.
        SELECT DISTINCT ON (s.code) s.*
        FROM jsonb_to_recordset(coalesce(_payload, '[]'::jsonb))
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
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.sync_person_statuses
    TO maw_media;
