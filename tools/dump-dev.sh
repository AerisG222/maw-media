#!/bin/bash
PSQLUSER=postgres
DB=maw_media

podman run -it --rm \
    --pod dev-media-pod \
    --name dev-media-pg-dump \
    --env "POSTGRES_PASSWORD_FILE=/secrets/psql-${PSQLUSER}" \
    --volume "/home/mmorano/maw/dev/media/data/pgpwd:/secrets" \
    docker.io/library/postgres:17 \
        pg_dump \
        -h localhost \
        -U "${PSQLUSER}" \
        -d "${DB}"
