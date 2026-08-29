#!/bin/bash
PSQLUSER=postgres
DB=maw_media

podman run --rm \
    --pod pod-maw-media \
    --name dev-media-pg-load \
    --security-opt label=disable \
    --env "POSTGRES_PASSWORD_FILE=/secrets/psql-${PSQLUSER}" \
    --volume "/home/mmorano/maw-media/dev/pg-secrets:/secrets" \
    --volume "$(pwd):/input" \
    docker.io/library/postgres:18-trixie \
        psql \
            -h localhost \
            -U "${PSQLUSER}" \
            -X \
            -f /input/roles.dev.dump

podman run --rm \
    --pod pod-maw-media \
    --name dev-media-pg-load \
    --security-opt label=disable \
    --env "POSTGRES_PASSWORD_FILE=/secrets/psql-${PSQLUSER}" \
    --volume "/home/mmorano/maw-media/dev/pg-secrets:/secrets" \
    --volume "$(pwd):/input" \
    docker.io/library/postgres:18-trixie \
        pg_restore \
            -h localhost \
            -U "${PSQLUSER}" \
            -d postgres \
            --create \
            --clean \
            --if-exists \
            /input/maw_media.dev.dump
