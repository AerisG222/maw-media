-- restricts one media to fewer roles than its category grants.
--
-- the problem it solves: a category is shared with a role as a whole, and a few
-- photos in an otherwise shareable category should not be.  before this the only
-- answer was to move them into a category of their own - often two photos,
-- splitting one event's story in half and leaving a tile that exists only to
-- carry a permission.
--
-- the semantics, which every reader of this table has to agree on:
--
--   * a media with no rows here follows its category, exactly as before.  that is
--     every media in the library until somebody restricts one, so the common case
--     costs nothing to read.
--
--   * a media with rows is visible only through those roles - and only where the
--     role is *also* granted the category.  the rows narrow what the category
--     allows; they can never widen it.  a row naming a role the category does not
--     grant simply grants nothing, rather than reaching into a category that role
--     was never given.
--
--   * it is decided per role, never per user.  a user holding both admin and
--     friend sees a photo restricted to admin, because one of their roles grants
--     it - which is why this lists the roles that *may* see a media rather than
--     the ones that may not.  a deny list evaluated per user would hide the photo
--     from that admin for also being a friend.
--
-- an allow list rather than a deny list for a second reason too: it fails closed.
-- a role created later sees no restricted photo until somebody says it should,
-- where a deny list would show it every hidden photo that nobody remembered to
-- exclude it from.
--
-- the rule is applied in exactly one place, media.user_media, and every read path
-- reaches media through it.
CREATE TABLE IF NOT EXISTS media.media_role (
    media_id UUID NOT NULL,
    role_id UUID NOT NULL,
    created TIMESTAMPTZ NOT NULL,
    created_by UUID NOT NULL,

    CONSTRAINT pk_media_media_role
    PRIMARY KEY (media_id, role_id),

    -- CASCADE: the restriction is a property of the media, and has nothing to say
    -- once the media is gone
    CONSTRAINT fk_media_media_role$media_media
    FOREIGN KEY (media_id)
    REFERENCES media.media(id)
    ON DELETE CASCADE,

    CONSTRAINT fk_media_media_role$media_role
    FOREIGN KEY (role_id)
    REFERENCES media.role(id),

    CONSTRAINT fk_media_media_role$media_user
    FOREIGN KEY (created_by)
    REFERENCES media.user(id)
);

GRANT SELECT, INSERT, DELETE
ON media.media_role
TO maw_media;
