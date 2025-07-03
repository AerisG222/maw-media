DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_catalog.pg_ts_dict
        WHERE dictname = 'english_hunspell'
    ) THEN

        -- note: see Containerfile
        -- en_us comes from the container build where hunspell-en-us is installed
        -- these resources are then used by postgres by calling pg_updatedicts to prepare them in $SHAREDIR/tsearch_data dir
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
