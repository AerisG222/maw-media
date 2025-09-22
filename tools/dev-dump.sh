#!/bin/bash
PSQLUSER=postgres
DB=maw_media

podman run --rm \
    --pod dev-media-pod \
    --name dev-media-pg-dumpall \
    --env "POSTGRES_PASSWORD_FILE=/secrets/psql-${PSQLUSER}" \
    --volume "/home/mmorano/maw-media/dev/pg-pwd:/secrets" \
    docker.io/library/postgres:17 \
        pg_dumpall \
            -h localhost \
            -U "${PSQLUSER}" \
            --roles-only \
    > roles.dev.dump

podman run --rm \
    --pod dev-media-pod \
    --name dev-media-pg-dump \
    --env "POSTGRES_PASSWORD_FILE=/secrets/psql-${PSQLUSER}" \
    --volume "/home/mmorano/maw-media/dev/pg-pwd:/secrets" \
    docker.io/library/postgres:17 \
        pg_dump \
            -Fc \
            -h localhost \
            -U "${PSQLUSER}" \
            -d "${DB}" \
    > maw_media.dev.dump
