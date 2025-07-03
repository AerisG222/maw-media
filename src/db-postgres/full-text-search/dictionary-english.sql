ALTER TEXT SEARCH CONFIGURATION	english
    ALTER MAPPING FOR
        asciiword,
        asciihword,
        hword_asciipart,
        word,
        hword,
        hword_part
    WITH
        xsyn,
        english_hunspell,
        english_stem;
