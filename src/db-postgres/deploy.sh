#!/bin/bash
DBNAME="maw_media"
IMAGE="docker.io/library/postgres:17"
PODNAME=$1
PWDFILEDIR=$2

function showUsage() {
    echo "deploy.sh <podname> <pwddir>"
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
    # header "pull latest postgres image"
    # podman pull "${IMAGE}"

    header "database ${DBNAME}"
    run_psql_script "database/maw_media.sql" "postgres" &> /dev/null

    header "roles"
    run_psql_script "roles/maw_media.sql"

    header "users"
    run_psql_script "users/svc_maw_media.sql"

    header "schemas"
    run_psql_script "schemas/media.sql"

    header "tables"
    run_psql_script "tables/media.scale.sql"
    run_psql_script "tables/media.location.sql"
    run_psql_script "tables/media.point_of_interest.sql"
    run_psql_script "tables/media.type.sql"
    run_psql_script "tables/media.user.sql"
    run_psql_script "tables/media.role.sql"
    run_psql_script "tables/media.external_identity.sql"
    run_psql_script "tables/media.user_role.sql"
    run_psql_script "tables/media.category.sql"
    run_psql_script "tables/media.category_favorite.sql"
    run_psql_script "tables/media.media.sql"
    run_psql_script "tables/media.category_media.sql"
    run_psql_script "tables/media.file.sql"
    run_psql_script "tables/media.category_role.sql"
    run_psql_script "tables/media.comment.sql"
    run_psql_script "tables/media.favorite.sql"

    header "views"
    run_psql_script "views/media.file_detail.sql"
    run_psql_script "views/media.user_category.sql"
    run_psql_script "views/media.user_media.sql"

    header "seed"
    run_psql_script "seed/media.type.sql"
    run_psql_script "seed/media.scale.sql"

    header "functions"
    run_psql_script "funcs/media.favorite_category.sql"
    run_psql_script "funcs/media.get_categories.sql"
    run_psql_script "funcs/media.get_category_media.sql"
    run_psql_script "funcs/media.get_category_years.sql"
    run_psql_script "funcs/media.get_random_media.sql"
    run_psql_script "funcs/media.set_category_teaser.sql"

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
