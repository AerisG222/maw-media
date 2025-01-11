ALTER TABLE media.category
    ADD CONSTRAINT fk_media_category$media_media
    FOREIGN KEY (teaser_media_id)
    REFERENCES media.media(id);

GRANT SELECT
ON media.category
TO maw_api;
