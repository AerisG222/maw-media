DO
$$
BEGIN

    IF NOT EXISTS (SELECT 1 FROM media.place_kind) THEN

        -- levels are spaced by 10 so a level can be slotted between two existing
        -- ones - a region between country and state, say - without renumbering
        -- rows that media.place already references.
        --
        -- city is deliberately the floor.  media.location also carries
        -- `neighborhood` and `sub_locality_level_1`, and an earlier draft treated
        -- a neighborhood level as likely; it was considered and rejected as too
        -- granular for what this browse is for.  those two columns feed the
        -- *city* fallback in media.assign_location_place instead, which is the
        -- only use they have here.
        INSERT INTO media.place_kind (code, label, level) VALUES ('country', 'Country',          10);
        INSERT INTO media.place_kind (code, label, level) VALUES ('state',   'State or Region',  20);
        INSERT INTO media.place_kind (code, label, level) VALUES ('city',    'City',             30);

    END IF;

END
$$
