-- verify hunspell (should return 'story')
SELECT * FROM TS_DEBUG('english', 'stories');

-- verify synonyms
SELECT * FROM TS_DEBUG('english', 'alyssa');
SELECT * FROM TS_DEBUG('english', 'sasa');
