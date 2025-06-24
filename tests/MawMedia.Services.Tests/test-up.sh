#! /bin/bash
PROJ_ROOT=~/git/maw-media
POD=integration-test-media-pod
CON=integration-test-media-pgsql
PWDDIR="$(pwd)/media-testing/pgpwd"
PGPWD=$(gpg --gen-random --armor 1 24 | base64)

mkdir -p "${PWDDIR}"
echo "${PGPWD}" > "${PWDDIR}/psql-postgres"

podman pod create \
    --name "${POD}" \
    --publish 9876:5432 \
    --replace

podman run \
    --detach \
    --pod "${POD}" \
    --name "${CON}" \
    --env "POSTGRES_PASSWORD_FILE=/secrets/psql-postgres" \
    --volume "${PWDDIR}:/secrets" \
    --security-opt label=disable \
    docker.io/library/postgres:17

sleep 2

podman run \
    --rm \
    --pod "${POD}" \
    --name "integration-test-media-createdb" \
    docker.io/library/postgres:17 \
        psql \
            -h 127.0.0.1 \
            -U postgres \
            -c "CREATE DATABASE maw_media;"

cd "${PROJ_ROOT}/src/db-postgres"
( "${PROJ_ROOT}/src/db-postgres/deploy.sh" "${POD}" "${PWDDIR}")
cd -
