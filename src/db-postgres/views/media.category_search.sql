CREATE MATERIALIZED VIEW IF NOT EXISTS media.category_search
AS
WITH category_search_data AS
(
    SELECT
        c.id AS category_id,
        c.name AS category_name,
        COALESCE(STRING_AGG (comm.body, ' '), '') AS comment_text,
        COALESCE(STRING_AGG(l.administrative_area_level_1, ' '), '') AS location_administrative_area_level_1,
        COALESCE(STRING_AGG(l.administrative_area_level_2, ' '), '') AS location_administrative_area_level_2,
        COALESCE(STRING_AGG(l.administrative_area_level_3, ' '), '') AS location_administrative_area_level_3,
        COALESCE(STRING_AGG(l.country, ' '), '') AS location_country,
        COALESCE(STRING_AGG(l.formatted_address, ' '), '') AS location_formatted_address,
        COALESCE(STRING_AGG(l.locality, ' '), '') AS location_locality,
        COALESCE(STRING_AGG(l.neighborhood, ' '), '') AS location_neighborhood,
        COALESCE(STRING_AGG(l.postal_code, ' '), '') AS location_postal_code,
        COALESCE(STRING_AGG(l.postal_code_suffix, ' '), '') AS location_postal_suffix,
        COALESCE(STRING_AGG(l.premise, ' '), '') AS location_premise,
        COALESCE(STRING_AGG(l.route, ' '), '') AS location_route,
        COALESCE(STRING_AGG(l.sub_locality_level_1, ' '), '') AS location_sub_locality_level_1,
        COALESCE(STRING_AGG(l.sub_locality_level_2, ' '), '') AS location_sub_locality_level_2,
        COALESCE(STRING_AGG(l.sub_premise, ' '), '') AS location_sub_premise,
        COALESCE(STRING_AGG(poi.type, ' '), '') AS poi_type,
        COALESCE(STRING_AGG(poi.name, ' '), '') AS poi_name
    FROM media.category c
    INNER JOIN media.category_media cm
        ON cm.category_id = c.id
    INNER JOIN media.media m
        ON cm.media_id = m.id
    LEFT OUTER JOIN media.comment comm
        ON comm.media_id = m.id
    LEFT OUTER JOIN media.location l
        ON l.id = COALESCE(m.location_override_id, m.location_id)
    LEFT OUTER JOIN media.point_of_interest poi
        ON poi.location_id = l.id
    GROUP BY
        c.id,
        c.name
)
SELECT
    category_id,
    SETWEIGHT(TO_TSVECTOR('english', category_name), 'A')
        || SETWEIGHT(TO_TSVECTOR('english', comment_text), 'B')
        || SETWEIGHT(TO_TSVECTOR('english', poi_type), 'B')
        || SETWEIGHT(TO_TSVECTOR('english', poi_name), 'B')
        || SETWEIGHT(TO_TSVECTOR('english', location_administrative_area_level_1), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_administrative_area_level_2), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_administrative_area_level_3), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_country), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_formatted_address), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_locality), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_neighborhood), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_postal_code), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_postal_suffix), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_premise), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_route), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_sub_locality_level_1), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_sub_locality_level_2), 'C')
        || SETWEIGHT(TO_TSVECTOR('english', location_sub_premise), 'C')
        AS search_vector
FROM category_search_data
ORDER BY category_id;

CREATE INDEX IF NOT EXISTS idx_media_category_search$search_vector
ON media.category_search
USING GIN (search_vector);

GRANT SELECT
ON media.category_search
TO maw_media;
