DO
$$
BEGIN

    IF NOT EXISTS (SELECT 1 FROM media.scale) THEN

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965306-0786-7af9-b3eb-a4dd6ef83505',
            'qqvg',
            160,
            120,
            false
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965306-0786-7af9-b3eb-a4dd6ef83606',
            'qqvg-fill',
            160,
            120,
            true
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965306-49b3-7212-b55d-345e7195a3b0',
            'qvg',
            320,
            240,
            false
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965306-49b3-7212-b55d-345e7195a6b6',
            'qvg-fill',
            320,
            240,
            true
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965306-6f04-739f-aea6-3b4022f1d2ce',
            'nhd',
            640,
            360,
            false
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965306-6f04-739f-aea6-3b4022f1d6c6',
            'nhd-fill',
            640,
            360,
            true
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965306-9387-7cb7-8945-1d626de296fa',
            'full-hd',
            1920,
            1080,
            false
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965306-b398-70e8-9bd4-af9e0bb96c8b',
            'qhd',
            2560,
            1440,
            false
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965306-d3b1-754a-abf1-be97f6b18a83',
            '4k',
            3840,
            2160,
            false
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965307-039f-732f-a768-c09584310119',
            '5k',
            5120,
            2880,
            false
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965307-20f9-7e01-955e-be53d178662d',
            '8k',
            7680,
            4320,
            false
        );

        INSERT INTO media.scale (
            id,
            code,
            width,
            height,
            fills_dimensions
        ) VALUES (
            '01965307-20f9-7e01-955e-be53d1786828',
            'src',
            999999,
            999999,
            false
        );
    END IF;

END
$$
