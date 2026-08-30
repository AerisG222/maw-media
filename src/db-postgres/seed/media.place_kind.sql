DO
$$
BEGIN

    IF NOT EXISTS (SELECT 1 FROM media.place_kind) THEN

        -- levels are spaced by 10 so a future level can be slotted between two
        -- existing ones - neighborhood between city and nothing, or region
        -- between country and state - without renumbering rows that
        -- media.place already references.  media.location carries `neighborhood`
        -- and `sub_locality_level_1` today, which media.assign_location_place
        -- currently collapses into the city fallback, so that is a live
        -- possibility rather than a hypothetical one.
        INSERT INTO media.place_kind (code, label, level) VALUES ('country', 'Country',          10);
        INSERT INTO media.place_kind (code, label, level) VALUES ('state',   'State or Region',  20);
        INSERT INTO media.place_kind (code, label, level) VALUES ('city',    'City',             30);

    END IF;

END
$$
