#!/bin/bash
PSQLUSER=postgres
DB=maw_media
SQLSCRIPT=src/db-postgres/loc2.sql

podman run -it --rm \
    --pod pod-maw-media \
    --name dev-media-pg-query \
    --env "POSTGRES_PASSWORD_FILE=/secrets/psql-${PSQLUSER}" \
    --volume "/home/mmorano/maw-media/dev/pg-secrets:/secrets" \
    --volume "/home/mmorano/git/maw-media:/code:ro" \
    --security-opt label=disable \
    docker.io/library/postgres:18-trixie \
        psql \
        -h localhost \
        -U "${PSQLUSER}" \
        -d "${DB}" \
        -f "/code/${SQLSCRIPT}"
