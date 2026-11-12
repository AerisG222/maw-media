-- 2026-11-12 - match the start of a word as well as the whole of it
--
-- turns what somebody typed into the search box into the tsquery
-- media.search_categories matches with.
--
-- plainto_tsquery alone only matches whole words.  the vectors hold 'alyssa', so
-- 'alyss' - a word somebody has simply not finished typing - found nothing at
-- all.  so each word here matches either the way plainto_tsquery would match it,
-- or as the start of a longer word: 'alyss' | 'alyss':*.  every word still has to
-- match, as before.
--
-- the prefix is taken from the word as typed, under the simple config, rather
-- than from its english lexeme.  stemming a word that is only half there gives a
-- stem the full word never has, which would then match nothing.  the english
-- form is still there alongside it, so a whole word keeps matching what it did -
-- 'birthdays' matches 'birthday' that way and not by the prefix.
--
-- returns NULL when nothing searchable was typed - only stop words, or only
-- punctuation - which matches nothing.
DROP FUNCTION IF EXISTS media.build_search_query;

CREATE OR REPLACE FUNCTION media.build_search_query
(
    _search_term TEXT
)
RETURNS TSQUERY
AS $$
DECLARE
    _word RECORD;
    _word_query TSQUERY;
    _prefix TSQUERY;
    _query TSQUERY;
BEGIN
    FOR _word IN
        SELECT LOWER(d.token) AS token
        FROM TS_DEBUG('english', COALESCE(_search_term, ''))
            WITH ORDINALITY d(alias, description, token, dictionaries, dictionary, lexemes, position)
        WHERE
            -- a hyphenated word arrives whole and then as each of its parts.  the
            -- parts are kept, so the whole would only repeat them.
            d.alias NOT IN ('asciihword', 'hword', 'numhword')
            -- an empty array is a stop word and NULL is punctuation or anything
            -- else the english config does not index.  either way it can not be
            -- in a vector, so it must not be required to be.
            AND CARDINALITY(d.lexemes) > 0
        -- each word once, in the order typed, so the same search always builds
        -- the same query
        GROUP BY LOWER(d.token)
        ORDER BY MIN(d.position)
    LOOP
        _word_query := PLAINTO_TSQUERY('english', _word.token);

        -- not below three characters, where the start of a word is too little to
        -- go on: 'ed' would find 'edinburgh' and everything else beginning that
        -- way.
        IF LENGTH(_word.token) >= 3 THEN
            -- built from plainto_tsquery's output rather than from the text, so
            -- what was typed never reaches the tsquery parser and quotes or
            -- operators in it are just characters.  only applied when that output
            -- is a single word - a url, say, comes back as several.
            _prefix := PLAINTO_TSQUERY('simple', _word.token);

            IF NUMNODE(_prefix) = 1 THEN
                _word_query := _word_query || (_prefix::TEXT || ':*')::TSQUERY;
            END IF;
        END IF;

        _query := CASE
            WHEN _query IS NULL THEN _word_query
            ELSE _query && _word_query
            END;
    END LOOP;

    RETURN _query;
END;
$$ LANGUAGE plpgsql STABLE;

GRANT EXECUTE
ON FUNCTION media.build_search_query
TO maw_media;
