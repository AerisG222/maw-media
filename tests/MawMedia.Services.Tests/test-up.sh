#! /bin/bash
PROJ_ROOT=~/git/maw-media
POD=integration-test-media-pod
CON=integration-test-media-pgsql
PGIMG="docker.io/library/postgres:18-trixie"
PWDDIR="$(pwd)/media-testing/pgpwd"

# the full text search config needs the hunspell dictionaries and the xsyn rules
# in the image's $SHAREDIR/tsearch_data.  mounted file by file rather than as a
# directory - a directory mount would hide the stock contents, and the
# english_hunspell dictionary reads english.stop from there.
TSEARCH_SRC="${PROJ_ROOT}/src/db-postgres/tsearch_data"
TSEARCH_DEST="/usr/share/postgresql/18/tsearch_data"
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
    --volume "${TSEARCH_SRC}/en_us.dict:${TSEARCH_DEST}/en_us.dict:ro" \
    --volume "${TSEARCH_SRC}/en_us.affix:${TSEARCH_DEST}/en_us.affix:ro" \
    --volume "${TSEARCH_SRC}/maw_media_xsyn.rules:${TSEARCH_DEST}/maw_media_xsyn.rules:ro" \
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
