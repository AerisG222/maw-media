CREATE OR REPLACE FUNCTION media.get_scales
()
RETURNS TABLE
(
    code TEXT,
    width INTEGER,
    height INTEGER,
    fills_dimensions BOOLEAN
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        s.code,
        s.width,
        s.height,
        s.fills_dimensions
    FROM media.scale s
    ORDER BY
        s.width DESC,
        s.code;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_scales
    TO maw_media;
