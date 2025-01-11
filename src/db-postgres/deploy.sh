#!/bin/bash
DBNAME="maw_media"
IMAGE="docker.io/library/postgres:17"
PODNAME=$1
PWDFILEDIR=$2

function showUsage() {
    echo "deploy.sh <podname> <pwdfile>"
}

function header() {
    echo "** ${1} **"
}

function run_psql_script() {
    local script=$1
    local db=$2

    if [ "${db}" == "" ]
    then
        db="${DBNAME}"
    fi

    if [ "${PODNAME}" == "" ]
    then
        psql -d "${db}" -q -f "${script}";
    else
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
                    -d "${db}" \
                    -q \
                    -f "/tmp/context/${script}"
    fi
}

function main() {
    header "pull latest postgres image"
    podman pull "${IMAGE}"

    header "database ${DBNAME}"
    run_psql_script "database/maw_media.sql" "postgres" &> /dev/null

    header "roles"
    run_psql_script "roles/maw_api.sql"

    header "users"
    run_psql_script "users/svc_maw_api.sql"

    header "schemas"
    run_psql_script "schemas/media.sql"

    header "tables"
    run_psql_script "tables/media.dimension.sql"
    run_psql_script "tables/media.location.sql"
    run_psql_script "tables/media.point_of_interest.sql"
    run_psql_script "tables/media.media_type.sql"
    run_psql_script "tables/media.role.sql"
    run_psql_script "tables/media.user.sql"
    run_psql_script "tables/media.external_identity.sql"
    run_psql_script "tables/media.user_role.sql"
    run_psql_script "tables/media.category.sql"
    run_psql_script "tables/media.media.sql"
    run_psql_script "tables/media.scaled_media.sql"
    run_psql_script "tables/media.category_role.sql"
    run_psql_script "tables/media.comment.sql"
    run_psql_script "tables/media.rating.sql"

    header "seed"

    header "functions"

    header "completed ${DBNAME}"
}

if [ "${PODNAME}" = "" ]; then
    showUsage
    exit
fi

if [ "${PWDFILEDIR}" = "" ]; then
    showUsage
    exit
fi

main
