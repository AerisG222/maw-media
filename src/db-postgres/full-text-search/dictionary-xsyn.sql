-- dependent on maw-media image placing the xsyn .rules file in $SHAREDIR/tsearch_data/ dir
ALTER TEXT SEARCH DICTIONARY xsyn
(
    MATCHSYNONYMS = true,
    RULES = maw_media_xsyn
);
