DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_catalog.pg_ts_dict
        WHERE dictname = 'english_hunspell'
    ) THEN

        -- en_us.dict / en_us.affix are committed under ../tsearch_data and are
        -- mounted into $SHAREDIR/tsearch_data of the stock postgres image.  see
        -- ../gen-tsearch-data.sh for how they are produced from hunspell-en-us.
        CREATE TEXT SEARCH DICTIONARY english_hunspell
        (
            template = ispell,
            DictFile = en_us,
            AffFile = en_us,
            StopWords = english
        );

    END IF;
END
$$;
