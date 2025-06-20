CREATE OR REPLACE FUNCTION media.get_category_years
(
    _user_id UUID
)
RETURNS TABLE
(
    year SMALLINT
)
AS $$
BEGIN
    RETURN QUERY
    WITH years AS
    (
        SELECT DISTINCT
            CAST(EXTRACT(YEAR FROM c.effective_date) AS SMALLINT) AS year
        FROM media.category c
        INNER JOIN media.user_category uc
            ON c.id = uc.category_id
            AND uc.user_id = _user_id
    )
    SELECT
        y.year
    FROM years y
    ORDER BY year DESC;
END

$$ LANGUAGE plpgsql;

GRANT EXECUTE
   ON FUNCTION media.get_category_years
   TO maw_media;
