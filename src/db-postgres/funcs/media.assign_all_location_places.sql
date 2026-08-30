-- derives places for every geocoded coordinate, and returns how many locations
-- ended up pointing at one.
--
-- both the one time backfill and the standing repair tool.  it is idempotent -
-- media.assign_location_place only writes when the answer changed - so the deploy
-- runs it unconditionally rather than tracking whether it has been run before,
-- and an operator can run it again after correcting media.place_alias to have the
-- corrections take effect across the whole library.
--
-- rows that have never been geocoded are skipped outright rather than resolved to
-- NULL.  they are 8% of the table and already carry a null place_id, so visiting
-- them would be 2,171 no-ops per run.  a coordinate that *loses* its country
-- later is still handled, because it no longer matches the filter only after
-- assign_location_place has cleared it - and the geocoder does not un-answer.
--
-- not admin gated, unlike the media.set_* functions.  it takes no user, reads no
-- one's data and can only ever recompute a derivation from the address text
-- already sitting in media.location, so there is nothing here to authorise
-- against.  the grant is what lets the test seeder derive places after inserting
-- its fixtures.
CREATE OR REPLACE FUNCTION media.assign_all_location_places()
RETURNS INTEGER
AS $$
DECLARE
    loc RECORD;
    assigned INTEGER := 0;
BEGIN
    FOR loc IN
        SELECT l.id
        FROM media.location l
        WHERE l.country IS NOT NULL
        ORDER BY l.id
    LOOP
        IF media.assign_location_place(loc.id) IS NOT NULL THEN
            assigned := assigned + 1;
        END IF;
    END LOOP;

    RETURN assigned;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.assign_all_location_places
    TO maw_media;
