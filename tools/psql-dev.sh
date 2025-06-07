#!/bin/bash
PSQLUSER=svc_maw_media
DB=maw_media

podman run -it --rm \
    --pod dev-media-pod \
    --name dev-api-pg-query \
    --env "POSTGRES_PASSWORD_FILE=/secrets/psql-${PSQLUSER}" \
    --volume "/home/mmorano/maw/dev/media/data/pgpwd:/secrets" \
    docker.io/library/postgres:17 \
        psql \
        -h localhost \
        -U "${PSQLUSER}" \
        -d "${DB}"
