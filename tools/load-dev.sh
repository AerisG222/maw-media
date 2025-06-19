#!/bin/bash
PSQLUSER=postgres
DB=maw_media

# create db before running
# define roles before running if you care about users/perms

podman run -it --rm \
    --pod dev-media-pod \
    --name dev-media-pg-load \
    --security-opt label=disable \
    --env "POSTGRES_PASSWORD_FILE=/secrets/psql-${PSQLUSER}" \
    --volume "/home/mmorano/maw/dev/media/data/pgpwd:/secrets" \
    --volume "/home/mmorano:/input" \
    docker.io/library/postgres:17 \
        psql \
        -X \
        -h localhost \
        -U "${PSQLUSER}" \
        -d "${DB}" \
        -f /input/maw-media.dev.dump
