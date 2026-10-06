CREATE OR REPLACE FUNCTION media.get_media_file
(
    _user_id UUID,
    _file_id UUID DEFAULT NULL,
    _path TEXT DEFAULT NULL,
    _exclude_src_files BOOLEAN = False
)
RETURNS TABLE
(
    file_id UUID,
    file_path TEXT,
    file_type TEXT,
    file_scale TEXT
)
AS $$
BEGIN
    IF _file_id IS NULL AND _path IS NULL THEN
        RAISE EXCEPTION 'Either file_id or path must be provided';
    END IF;

    -- 2026-09-20 - find the file first, then ask whether the caller may have it.
    --
    -- it used to read the other way round - every media the caller can see, joined
    -- to the file - and that is the expensive direction by a wide margin.
    -- media.user_media is a DISTINCT over (category, media, slug, user), and a
    -- join cannot be pushed inside one, so postgres built all 168,818 rows and
    -- spilled the dedupe to disk before matching the single file asked for.  that
    -- was 238ms per call on the dev restore against 5ms this way.
    --
    -- it is per call that makes it matter rather than the number itself.  this is
    -- what authorizes /assets, so it runs once per thumbnail on a cache miss - a
    -- grid of fifty was around eleven seconds of database time to draw.
    --
    -- both entry points are indexed on the file's own identity: pk_media_file for
    -- an id, ix_media_file$path for a path.
    --
    -- EXISTS rather than a join, and that is the second half of the change: a
    -- media sitting in several categories the caller can see used to return the
    -- same file once per category.  the question here is only whether *some*
    -- category grants access, so one row comes back either way.
    --
    -- 2026-10-06 - the EXISTS asks media.user_media rather than composing
    -- category access itself.  it did that while media.user_media carried a
    -- DISTINCT, which is gone; and a media may now be restricted to fewer roles
    -- than its category grants, which only media.user_media knows about.  this is
    -- the function that authorizes /assets, so it is the last place that rule
    -- could be allowed to go missing.
    RETURN QUERY
    SELECT
        md.file_id,
        md.file_path,
        md.file_type,
        md.file_scale
    FROM media.media_detail md
    WHERE
        (
            _exclude_src_files = FALSE
            OR
            md.file_scale <> 'src'
        )
        AND
        (_file_id IS NULL OR md.file_id = _file_id)
        AND
        (_path IS NULL OR md.file_path = _path)
        AND EXISTS
        (
            SELECT 1
            FROM media.user_media um
            WHERE
                um.media_id = md.media_id
                AND um.user_id = _user_id
        );
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE
    ON FUNCTION media.get_media_file
    TO maw_media;
