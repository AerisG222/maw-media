#!/bin/bash
PSQLUSER=postgres
DB=maw_media

podman run -it --rm \
    --pod pod-maw-media \
    --name dev-media-pg-query \
    --env "POSTGRES_PASSWORD_FILE=/secrets/psql-${PSQLUSER}" \
    --volume "/home/mmorano/maw-media/dev/pg-secrets:/secrets" \
    docker.io/library/postgres:18-trixie \
        psql \
        -h localhost \
        -U "${PSQLUSER}" \
        -d "${DB}"
