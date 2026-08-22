CREATE TABLE IF NOT EXISTS media.person_favorite (
    created_by UUID NOT NULL,
    person_id UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_media_person_favorite
    PRIMARY KEY (created_by, person_id),

    CONSTRAINT fk_media_person_favorite$media_user
    FOREIGN KEY (created_by)
    REFERENCES media.user(id),

    -- CASCADE, unlike the category and media favourites, because people are
    -- published data: media.delete_persons hard deletes a cluster that no longer
    -- exists upstream, and a favourite must not be able to block that.  losing
    -- the favourite is correct - the person it pointed at is gone.
    CONSTRAINT fk_media_person_favorite$media_person
    FOREIGN KEY (person_id)
    REFERENCES media.person(id)
    ON DELETE CASCADE
);

GRANT SELECT, INSERT, DELETE
ON media.person_favorite
TO maw_media;
