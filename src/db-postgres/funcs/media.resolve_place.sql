-- finds or creates the place one level of an address resolves to.
--
-- the single point at which a media.place row comes into existence.  callers pass
-- an already normalized match key and the raw name to display, and get back an
-- id; whether that meant a lookup or an insert is not their concern.
--
-- the match is always against media.place_alias, never against media.place.name.
-- that is the whole point of the alias table: an admin who renames a place, or
-- merges two the geocoder spelled differently, would otherwise be undone the next
-- time this function ran over one of the affected coordinates.  a new place gets
-- an identity alias here, so the ordinary path and the corrected path are one
-- code path rather than a rule plus an exception.
--
-- _parent_id scopes the match, and IS NOT DISTINCT FROM is load bearing in the
-- lookup below: every country has a null parent, and `parent_place_id = NULL`
-- matches nothing, so plain equality would miss the existing row every time and
-- try to insert a duplicate on every call.
--
-- the retry loop handles two sessions racing to create the same place - the
-- backfill running while the correction worker publishes, say.  the loser catches
-- the unique violation on the alias and goes round again, where the winner's row
-- is now visible.  it cannot spin: the second pass either finds a row or fails
-- for a different reason, which propagates.
CREATE OR REPLACE FUNCTION media.resolve_place
(
    _kind TEXT,
    _parent_id UUID,
    _match_key TEXT,
    _name TEXT
)
RETURNS UUID
AS $$
DECLARE
    found_id UUID;
    new_id UUID;
    new_slug TEXT;
BEGIN
    IF _match_key IS NULL THEN
        RETURN NULL;
    END IF;

    LOOP
        SELECT pa.place_id
        INTO found_id
        FROM media.place_alias pa
        WHERE pa.kind = _kind
            AND pa.parent_place_id IS NOT DISTINCT FROM _parent_id
            AND pa.match_key = _match_key;

        IF found_id IS NOT NULL THEN
            RETURN found_id;
        END IF;

        BEGIN
            -- v7 rather than v4, matching the Guid.CreateVersion7() the
            -- application uses everywhere else, so places sort by creation the
            -- way every other id in this schema does
            new_id := uuidv7();
            new_slug := media.build_place_slug(_name, new_id);

            -- the slug is unique per parent, and two different names under one
            -- parent can reduce to the same one.  suffixing with the head of the
            -- uuid resolves it in a single extra probe rather than counting
            -- upwards, and it cannot itself collide.
            IF EXISTS (
                SELECT 1
                FROM media.place p
                WHERE p.parent_id IS NOT DISTINCT FROM _parent_id
                    AND p.slug = new_slug
            ) THEN
                new_slug := new_slug || '-' || LEFT(new_id::TEXT, 8);
            END IF;

            INSERT INTO media.place (id, parent_id, kind, name, slug, created, modified)
            VALUES (new_id, _parent_id, _kind, BTRIM(_name), new_slug, NOW(), NOW());

            INSERT INTO media.place_alias (place_id, kind, parent_place_id, match_key, created)
            VALUES (new_id, _kind, _parent_id, _match_key, NOW());

            RETURN new_id;
        EXCEPTION WHEN unique_violation THEN
            -- another session got there first; go round and read its row
            NULL;
        END;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.resolve_place
    TO maw_media;
