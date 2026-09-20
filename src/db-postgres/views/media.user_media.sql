-- which media a user may see, and in which category.
--
-- 2025-11-04 - add slug to return
-- 2026-09-20 - drop the DISTINCT
--
-- it deduplicated nothing, and it was not free.  the two inputs already make
-- every row unique: media.user_category is itself a DISTINCT over
-- (category_id, user_id), and media.category_media has a primary key on
-- (category_id, media_id) - so joining them yields one row per
-- (category_id, media_id, user_id) by construction.  media_slug is a column of
-- that same row and adds nothing.  verified against the library: 5,746,548 rows
-- with the DISTINCT and 5,746,548 without.
--
-- what it cost is the part worth recording.  postgres cannot push a join
-- condition into a subquery with a DISTINCT, so every caller that *joined* this
-- view - rather than filtering it by a constant - had to build all 5.7M rows and
-- deduplicate them, spilling to disk at the default work_mem, before the join
-- could discard all but a handful.  that was the hidden second half of most of
-- the slow reads here: a city page, a person's photos and a clan listing each
-- paid it whole.
--
-- media.user_face and media.user_location keep their own DISTINCT, and those are
-- not redundant in the same way: both project the category away, so a media
-- filed in two categories a caller can see really would arrive twice.
--
-- the CASCADE drops media.user_face and media.user_location, which are rebuilt
-- on it - deploy.sh queues them immediately after this file for that reason.
DROP VIEW IF EXISTS media.user_media CASCADE;

CREATE OR REPLACE VIEW media.user_media AS
    SELECT
        cm.category_id,
        cm.media_id,
        cm.slug AS media_slug,
        uc.user_id
    FROM media.user_category uc
    INNER JOIN media.category_media cm
        ON uc.category_id = cm.category_id;

GRANT SELECT
ON media.user_media
TO maw_media;
