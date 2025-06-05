#!/bin/bash
PODNAME="dev-media-pod"
PWDFILEDIR="/home/mmorano/maw/dev/media/data/pgpwd"
DBNAME="maw_media"
IMAGE="docker.io/library/postgres:17"

function run_psql_script() {
    local script=$1

    podman run -it --rm \
        --pod "${PODNAME}" \
        --env "POSTGRES_PASSWORD_FILE=/secrets/psql-postgres" \
        --volume "${PWDFILEDIR}":/secrets:ro \
        --volume "$(pwd)":/tmp/context:ro \
        --security-opt label=disable \
        "${IMAGE}" \
            psql \
                -h 127.0.0.1 \
                -U postgres \
                -d "${DBNAME}" \
                -q \
                -f "${script}"
}

run_psql_script "/tmp/context/blog.post.sql"
