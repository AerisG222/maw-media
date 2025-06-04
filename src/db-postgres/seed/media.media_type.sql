DO
$$
BEGIN

    IF NOT EXISTS (SELECT 1 FROM media.media_type) THEN

        INSERT INTO media.media_type (id, name) VALUES ('01964f94-fa50-7846-b2e6-26d4609cc972', 'photo');
        INSERT INTO media.media_type (id, name) VALUES ('01964f94-fa51-705b-b0e2-b4c668ac6fab', 'video');
        INSERT INTO media.media_type (id, name) VALUES ('01964f94-fa51-705b-b0e2-b4c668ac6fcd', 'video-poster');

    END IF;

END
$$
