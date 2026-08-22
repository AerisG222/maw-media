#!/bin/bash
DBNAME="maw_media"
IMAGE="docker.io/library/postgres:18-trixie"
PODNAME=$1
PWDFILEDIR=$2

# the scripts are all written to be re-runnable (CREATE ... IF NOT EXISTS), so a
# repeat deploy emits a NOTICE for every object that is already there.  raising
# the client threshold to warning drops that noise and leaves warnings and
# errors standing out.
PSQL_OPTIONS="-c client_min_messages=warning"

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
        PGOPTIONS="${PSQL_OPTIONS}" psql -d "${db}" -q -v ON_ERROR_STOP=1 -f "${script}"
        status=$?
    else
        podman run --rm \
            --pod "${PODNAME}" \
            --env "POSTGRES_PASSWORD_FILE=/secrets/psql-postgres" \
            --env "PGOPTIONS=${PSQL_OPTIONS}" \
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

# postgres may still be starting when a deploy begins - the integration test
# harness starts a container and deploys straight after a short sleep.  the very
# first script would then fail to connect, and since any failure now aborts the
# run, a startup race would read as a broken deploy.
function wait_for_postgres() {
    local attempts=60
    local i=1

    while [ ${i} -le ${attempts} ]
    do
        if postgres_is_ready
        then
            return 0
        fi

        sleep 1
        i=$((i + 1))
    done

    echo "" >&2
    echo "** DEPLOY FAILED: postgres was not reachable after ${attempts} attempts **" >&2
    exit 1
}

function postgres_is_ready() {
    if [ "${PODNAME}" == "" ]
    then
        psql -d postgres -q -c "SELECT 1" > /dev/null 2>&1
    else
        podman run --rm \
            --pod "${PODNAME}" \
            --env "POSTGRES_PASSWORD_FILE=/secrets/psql-postgres" \
            --volume "${PWDFILEDIR}":/secrets:ro \
            --security-opt label=disable \
            "${IMAGE}" \
                psql \
                    -h 127.0.0.1 \
                    -U postgres \
                    -d postgres \
                    -q \
                    -c "SELECT 1" > /dev/null 2>&1
    fi
}

function main() {
    wait_for_postgres

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
    run_psql_script "tables/media.person_favorite.sql"
    run_psql_script "tables/media.clan.sql"
    run_psql_script "tables/media.clan_person.sql"
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
    run_psql_script "views/media.user_face.sql"

    header "seed"
    run_psql_script "seed/media.type.sql"
    run_psql_script "seed/media.scale.sql"

    header "functions"
    run_psql_script "funcs/media.add_comment.sql"
    run_psql_script "funcs/media.bulk_set_media_gps_override.sql"
    run_psql_script "funcs/media.create_clan.sql"
    run_psql_script "funcs/media.create_external_identity.sql"
    run_psql_script "funcs/media.delete_clan.sql"
    run_psql_script "funcs/media.delete_faces.sql"
    run_psql_script "funcs/media.delete_persons.sql"
    run_psql_script "funcs/media.favorite_category.sql"
    run_psql_script "funcs/media.favorite_media.sql"
    run_psql_script "funcs/media.favorite_person.sql"
    run_psql_script "funcs/media.fix_inaccurate_location.sql"
    run_psql_script "funcs/media.get_categories.sql"
    run_psql_script "funcs/media.get_categories_without_gps.sql"
    run_psql_script "funcs/media.get_category_media.sql"
    run_psql_script "funcs/media.get_category_years.sql"
    run_psql_script "funcs/media.get_clans.sql"
    run_psql_script "funcs/media.get_comments.sql"
    run_psql_script "funcs/media.get_face_exists.sql"
    run_psql_script "funcs/media.get_inaccurate_locations.sql"
    run_psql_script "funcs/media.get_is_admin.sql"
    run_psql_script "funcs/media.get_locations_without_metadata.sql"
    run_psql_script "funcs/media.get_media.sql"
    run_psql_script "funcs/media.get_media_file.sql"
    run_psql_script "funcs/media.get_media_gps.sql"
    run_psql_script "funcs/media.get_metadata.sql"
    run_psql_script "funcs/media.get_person_media.sql"
    run_psql_script "funcs/media.get_persons.sql"
    run_psql_script "funcs/media.get_random_media.sql"
    run_psql_script "funcs/media.get_scales.sql"
    run_psql_script "funcs/media.get_stats.sql"
    run_psql_script "funcs/media.get_stats_for_year.sql"
    run_psql_script "funcs/media.get_user_can_view_face.sql"
    run_psql_script "funcs/media.get_visible_person_count.sql"
    run_psql_script "funcs/media.get_user_state.sql"
    run_psql_script "funcs/media.search_categories.sql"
    run_psql_script "funcs/media.set_category_teaser.sql"
    run_psql_script "funcs/media.set_clan_persons.sql"
    run_psql_script "funcs/media.set_location_metadata.sql"
    run_psql_script "funcs/media.set_media_gps_override.sql"
    run_psql_script "funcs/media.set_point_of_interest.sql"
    run_psql_script "funcs/media.sync_faces.sql"
    run_psql_script "funcs/media.sync_person_statuses.sql"
    run_psql_script "funcs/media.sync_persons.sql"
    run_psql_script "funcs/media.update_clan.sql"

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
