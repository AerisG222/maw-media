-- resolves one coordinate to the place a user browses it by, and records it on
-- media.location.place_id.
--
-- the sole writer of that column.  every path that puts reverse geocode text on a
-- location calls this afterwards - media.set_location_metadata when the geocoder
-- answers, and media.fix_inaccurate_location when it copies metadata onto a
-- corrected coordinate - so a location's place never lags its address.
--
-- the levels, and what a missing one means:
--
--   country  country                        absent -> place_id is NULL and the
--                                           coordinate is not browsable at all
--   state    administrative_area_level_1    absent -> the level is skipped and
--                                           the city hangs off the country
--   city     locality, else                 absent -> the deepest node is the
--            sub_locality_level_1, else     state, or the country
--            administrative_area_level_2
--
-- the city fallback chain earns its keep on non-us addresses, where google puts
-- the city somewhere other than locality.  the audit measured how much: locality
-- supplies 25,666 of them, sub_locality_level_1 three, and
-- administrative_area_level_2 twenty four.  a small tail, but the alternative is
-- those coordinates browsing as a bare state.
--
-- place_id points at the *deepest* node only.  ancestors come from the parent
-- chain, so a row cannot end up claiming a city that sits in a different state
-- than the one beside it.
--
-- re-running is safe and is the intended way to repair: the function recomputes
-- from whatever the address says now, so a re-geocode moves the coordinate to its
-- new place.  a place left with no locations behind such a move is not collected
-- here - it simply stops appearing, since media.get_places joins through to
-- visible media and a place with none drops out.
CREATE OR REPLACE FUNCTION media.assign_location_place
(
    _location_id UUID
)
RETURNS UUID
AS $$
DECLARE
    raw_country TEXT;
    raw_state TEXT;
    raw_city TEXT;
    country_id UUID;
    state_id UUID;
    parent_id UUID;
    deepest_id UUID;
BEGIN
    SELECT
        l.country,
        l.administrative_area_level_1,
        COALESCE(l.locality, l.sub_locality_level_1, l.administrative_area_level_2)
    INTO
        raw_country,
        raw_state,
        raw_city
    FROM media.location l
    WHERE l.id = _location_id;

    IF NOT FOUND THEN
        RETURN NULL;
    END IF;

    -- no country, no hierarchy to hang anything from.  this is the 2,171 rows
    -- awaiting a geocode, and it is a normal state rather than an error - the
    -- correction worker fills them in over time and each becomes browsable then.
    country_id := media.resolve_place(
        'country',
        NULL,
        media.normalize_place_name(raw_country),
        raw_country
    );

    IF country_id IS NULL THEN
        UPDATE media.location
        SET place_id = NULL
        WHERE id = _location_id
            AND place_id IS NOT NULL;

        RETURN NULL;
    END IF;

    state_id := media.resolve_place(
        'state',
        country_id,
        media.normalize_place_name(raw_state),
        raw_state
    );

    -- a null state collapses the level rather than inventing a placeholder: Macao
    -- and Hong Kong genuinely have no state, and 48 rows in the audit prove the
    -- geocoder agrees.  their cities parent to the country instead.
    parent_id := COALESCE(state_id, country_id);

    deepest_id := COALESCE(
        media.resolve_place(
            'city',
            parent_id,
            media.normalize_place_name(raw_city),
            raw_city
        ),
        parent_id
    );

    -- guarded so a repeat run over an unchanged address writes nothing.  without
    -- it the backfill would rewrite all 27,870 rows on every deploy and leave a
    -- dead tuple behind each one, for no change in the answer.
    UPDATE media.location
    SET place_id = deepest_id
    WHERE id = _location_id
        AND place_id IS DISTINCT FROM deepest_id;

    RETURN deepest_id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.assign_location_place
    TO maw_media;
