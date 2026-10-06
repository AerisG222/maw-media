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
-- 2026-10-06 - honour media.media_role
--
-- a media may now be restricted to fewer roles than its category grants; see
-- tables/media.media_role.sql for the semantics.  this view is the one place that
-- rule is applied, and every read path - places, faces, random media, the asset
-- check itself - reaches media through it.  a path that composed category level
-- access for itself would quietly hand out restricted photos, which is why those
-- were all rerouted here first.
--
-- the shape of the rule is the whole of the engineering here, and two obvious
-- shapes were built, measured on the dev restore, and rejected:
--
--   * two branches under UNION ALL, restricted and unrestricted.  postgres plans
--     a UNION ALL view as an append of separate subqueries, so a caller's join
--     cannot reach inside it: checking one file materialized 165,737 rows to find
--     it, and a city page went from 26ms to 1.3s.
--
--   * one branch filtered by `NOT IN (restricted) OR EXISTS (role grants it)`.
--     correct, and fast in isolation - but a correlated subplan in a WHERE is
--     parallel restricted, so any query reading this view lost its parallel
--     workers.  the root place listing went from ~305ms to ~440ms with nothing
--     restricted at all.  a NOT IN over a LEFT JOIN, and a denormalized flag
--     column, kept the subplan and measured the same.
--
-- what is left is one branch whose filter is plain column tests, which are
-- parallel safe and push into a caller like any other join:
--
--   restricted  the media that carry any restriction.  DISTINCT, so at most one
--               row per media, and tiny - hashed once per query.
--
--   allowed     the (category, media, user) triples where one of the user's
--               roles is both on the media's list and granted the category.
--               DISTINCT, so a user holding two qualifying roles still matches
--               once, and small - restricted media times the users who hold a
--               qualifying role.  joined through category_media so a role only
--               counts where it is granted the media's own category: the
--               restriction narrows the category, it never widens it.
--
-- a row survives when it is unrestricted, or restricted and allowed.  the two
-- DISTINCTs are barriers only around these small inputs; the view itself is a
-- single SELECT without one, so it still flattens into its callers, and every
-- row is still unique on (category_id, media_id, user_id) for the reason above.
--
-- measured against the previous view in the same sitting: identical results
-- across 35 checksums of every read path built on it, the root place listing
-- and city pages flat, the asset check +0.3ms, the face check +0.6ms, and random
-- media +8-10ms - with 50 media restricted as with none.
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
        ON uc.category_id = cm.category_id
    LEFT OUTER JOIN
    (
        SELECT DISTINCT mr.media_id
        FROM media.media_role mr
    ) restricted
        ON restricted.media_id = cm.media_id
    LEFT OUTER JOIN
    (
        SELECT DISTINCT
            rcm.category_id,
            rcm.media_id,
            ur.user_id
        FROM media.media_role mr
        INNER JOIN media.category_media rcm
            ON rcm.media_id = mr.media_id
        INNER JOIN media.category_role cr
            ON cr.category_id = rcm.category_id
            AND cr.role_id = mr.role_id
        INNER JOIN media.user_role ur
            ON ur.role_id = mr.role_id
    ) allowed
        ON allowed.category_id = cm.category_id
        AND allowed.media_id = cm.media_id
        AND allowed.user_id = uc.user_id
    WHERE restricted.media_id IS NULL
        OR allowed.media_id IS NOT NULL;

GRANT SELECT
ON media.user_media
TO maw_media;
