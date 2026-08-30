-- derive places for any coordinate that does not have one yet.
--
-- runs on every deploy rather than once, because it is idempotent and because
-- that is what makes a schema change to the derivation take effect: edit
-- media.assign_location_place, deploy, and the whole library is reconciled to the
-- new rules without a separate migration step to remember.
--
-- see docs/browse-by-location.md
DO
$$
DECLARE
    assigned INTEGER;
BEGIN
    assigned := media.assign_all_location_places();

    RAISE NOTICE 'places derived for % locations', assigned;
END
$$;
