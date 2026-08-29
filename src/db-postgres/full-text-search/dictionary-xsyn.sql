-- depends on ../tsearch_data/maw_media_xsyn.rules being mounted into the
-- $SHAREDIR/tsearch_data dir of the postgres container
ALTER TEXT SEARCH DICTIONARY xsyn
(
    MATCHSYNONYMS = true,
    RULES = maw_media_xsyn
);
