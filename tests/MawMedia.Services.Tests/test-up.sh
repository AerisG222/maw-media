#! /bin/bash
PROJ_ROOT=~/git/maw-media
POD=integration-test-media-pod
CON=integration-test-media-pgsql
PGIMG="docker.io/aerisg222/maw-media-postgres:latest"
PWDDIR="$(pwd)/media-testing/pgpwd"
PGPWD=$(gpg --gen-random --armor 1 24 | base64)
MEDIAPWD=$(gpg --gen-random --armor 1 24 | base64)

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
    "${PGIMG}"

sleep 2

cd "${PROJ_ROOT}/src/db-postgres"
( "${PROJ_ROOT}/src/db-postgres/deploy.sh" "${POD}" "${PWDDIR}")
cd -

echo "${MEDIAPWD}" > "${PWDDIR}/psql-svc_maw_media"

podman run \
    --rm \
    --pod "${POD}" \
    --name "integration-test-media-passwd" \
    "${PGIMG}" \
        psql \
            -h 127.0.0.1 \
            -U postgres \
            -c "ALTER USER svc_maw_media WITH PASSWORD '${MEDIAPWD}';"
