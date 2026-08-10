#!/bin/bash
DBNAME="maw_media"
IMAGE="docker.io/library/postgres:18-trixie"
PODNAME=$1
PWDFILEDIR=$2

function showUsage() {
    echo "deploy.sh <podname> <pwddir>"
}

function header() {
    echo "** ${1} **"
}

# runs a script with ON_ERROR_STOP so psql aborts on the first error and exits
# non-zero.  without it psql reports success no matter what failed, which lets a
# broken schema deploy silently - a table that already exists under a different
# definition, for example, skips its CREATE and then fails every index and
# constraint that follows.
#
# any failure aborts the whole deploy: the scripts are ordered by dependency, so
# continuing past a failure only produces more failures against a schema that is
# already wrong.  pass "allow_failure" as the third argument for the cases where
# a non-zero exit is expected (creating a database that is already there).
function run_psql_script() {
    local script=$1
    local db=$2
    local allow_failure=$3
    local status=0

    echo "    - $script"

    if [ "${db}" == "" ]
    then
        db="${DBNAME}"
    fi

    if [ "${PODNAME}" == "" ]
    then
        psql -d "${db}" -q -v ON_ERROR_STOP=1 -f "${script}"
        status=$?
    else
        podman run --rm \
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
                    -v ON_ERROR_STOP=1 \
                    -f "/tmp/context/${script}"
        status=$?

        sleep 1
    fi

    if [ ${status} -ne 0 ] && [ "${allow_failure}" != "allow_failure" ]
    then
        echo "" >&2
        echo "** DEPLOY FAILED: ${script} (exit ${status}) **" >&2
        exit 1
    fi

    return 0
}

function main() {
    # header "pull latest postgres image"
    # podman pull "${IMAGE}"

    header "database ${DBNAME}"
    # CREATE DATABASE has no IF NOT EXISTS, so this is expected to fail on every
    # run after the first
    run_psql_script "database/maw_media.sql" "postgres" "allow_failure" &> /dev/null

    header "full text search"
    run_psql_script "full-text-search/extension-dict_xsyn.sql"
    run_psql_script "full-text-search/dictionary-xsyn.sql"
    run_psql_script "full-text-search/dictionary-english_hunspell.sql"
    run_psql_script "full-text-search/dictionary-english.sql"

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
    run_psql_script "tables/media.person_status.sql"
    run_psql_script "tables/media.person.sql"
    run_psql_script "tables/media.face.sql"

    header "views"
    run_psql_script "views/media.category_search.sql"
    run_psql_script "views/media.category_stats.sql"
    run_psql_script "views/media.file_detail.sql"
    run_psql_script "views/media.media_detail.sql"
    run_psql_script "views/media.media_exif_gps.sql"
    run_psql_script "views/media.media_gps.sql"
    run_psql_script "views/media.user_category.sql"
    run_psql_script "views/media.user_media.sql"

    header "seed"
    run_psql_script "seed/media.type.sql"
    run_psql_script "seed/media.scale.sql"

    header "functions"
    run_psql_script "funcs/media.add_comment.sql"
    run_psql_script "funcs/media.bulk_set_media_gps_override.sql"
    run_psql_script "funcs/media.create_external_identity.sql"
    run_psql_script "funcs/media.favorite_category.sql"
    run_psql_script "funcs/media.favorite_media.sql"
    run_psql_script "funcs/media.fix_inaccurate_location.sql"
    run_psql_script "funcs/media.get_categories.sql"
    run_psql_script "funcs/media.get_categories_without_gps.sql"
    run_psql_script "funcs/media.get_category_media.sql"
    run_psql_script "funcs/media.get_category_years.sql"
    run_psql_script "funcs/media.get_comments.sql"
    run_psql_script "funcs/media.get_inaccurate_locations.sql"
    run_psql_script "funcs/media.get_is_admin.sql"
    run_psql_script "funcs/media.get_locations_without_metadata.sql"
    run_psql_script "funcs/media.get_media.sql"
    run_psql_script "funcs/media.get_media_file.sql"
    run_psql_script "funcs/media.get_media_gps.sql"
    run_psql_script "funcs/media.get_metadata.sql"
    run_psql_script "funcs/media.get_random_media.sql"
    run_psql_script "funcs/media.get_scales.sql"
    run_psql_script "funcs/media.get_stats.sql"
    run_psql_script "funcs/media.get_stats_for_year.sql"
    run_psql_script "funcs/media.get_user_state.sql"
    run_psql_script "funcs/media.search_categories.sql"
    run_psql_script "funcs/media.set_category_teaser.sql"
    run_psql_script "funcs/media.set_location_metadata.sql"
    run_psql_script "funcs/media.set_media_gps_override.sql"
    run_psql_script "funcs/media.set_point_of_interest.sql"
    run_psql_script "funcs/media.sync_faces.sql"

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
