#!/bin/bash
DBNAME="maw_media"
IMAGE="docker.io/library/postgres:18-trixie"
PODNAME=$1
PWDFILEDIR=$2

# the scripts are all written to be re-runnable (CREATE ... IF NOT EXISTS), so a
# repeat deploy emits a NOTICE for every object that is already there.  raising
# the client threshold to warning drops that noise and leaves warnings and
# errors standing out.
#
# lock_timeout turns a stalled deploy into a failed one.  several scripts take an
# ACCESS EXCLUSIVE lock - any ALTER TABLE ... ADD COLUMN or ADD CONSTRAINT does -
# and that conflicts with the ACCESS SHARE lock every plain SELECT holds.  so one
# idle transaction or one long running query somewhere makes the deploy wait, and
# because postgres queues later lock requests behind a pending exclusive one, the
# whole run appears to stop dead on whichever script asked first rather than on
# the session actually at fault.
#
# with a timeout it fails instead, and ON_ERROR_STOP plus psql's include tracking
# then name the script and line - which is the difference between "the deploy is
# hung" and a message pointing at media.location.sql.  30s is well beyond what
# these locks need on an idle database and short enough not to read as a hang.
#
# to find the culprit when it fires:
#   SELECT pid, pg_blocking_pids(pid), state, query FROM pg_stat_activity
#   WHERE cardinality(pg_blocking_pids(pid)) > 0;
PSQL_OPTIONS="-c client_min_messages=warning -c lock_timeout=30s"

# where the sql lives from psql's point of view.  empty when psql runs on this
# host, since the scripts are then addressed relative to the working directory.
CONTEXT_PREFIX=""

# the deploy is assembled as a single psql script of \i includes and executed in
# one session, rather than one psql invocation per file.
#
# the reason is container churn.  there are ~86 scripts, and a container per
# script meant ~86 create/teardown cycles per deploy - each mounting the postgres
# image, each writing to podman's database.  podman removes a --rm container in a
# separate step after it exits, so anything that interrupts a run (a failed
# script, ctrl-c, an OOM) strands containers permanently, and `podman ps -a`
# slows down in proportion to how many have piled up.  two containers per deploy
# cannot meaningfully accumulate.
#
# semantics are unchanged: ON_ERROR_STOP still aborts on the first error, and
# psql reports the *included* file and line, so a failure still names the script
# that caused it.  no script sets session state - there is no top level BEGIN,
# no session level SET and no \c anywhere in the tree - so sharing one session
# between them changes nothing.
DRIVER=""

function showUsage() {
    echo "deploy.sh <podname> <pwddir>"
}

function header() {
    DRIVER+="\\echo ''"$'\n'
    DRIVER+="\\echo '** ${1} **'"$'\n'
}

function queue() {
    DRIVER+="\\echo '    - ${1}'"$'\n'
    DRIVER+="\\i ${CONTEXT_PREFIX}${1}"$'\n'
}

# runs whatever has been queued so far, in one psql session, and aborts the
# deploy if any of it fails.  the scripts are ordered by dependency, so
# continuing past a failure only produces more failures against a schema that is
# already wrong.
function run_driver() {
    local status=0

    printf '%s' "${DRIVER}" | psql_run "${DBNAME}"
    status=$?

    DRIVER=""

    if [ ${status} -ne 0 ]
    then
        echo "" >&2
        echo "** DEPLOY FAILED (exit ${status}) - see the psql error above for the script and line **" >&2
        exit 1
    fi
}

# one script, on its own, for the case where a non-zero exit is expected.
# CREATE DATABASE has no IF NOT EXISTS, so it fails on every run after the first.
function run_single_script() {
    local script=$1
    local db=$2

    echo "    - ${script}"

    printf '%s' "\\i ${CONTEXT_PREFIX}${script}"$'\n' | psql_run "${db}"

    return 0
}

# reads the script to run from stdin.  -i on podman run is load bearing: without
# it the container gets no stdin and psql silently does nothing.
function psql_run() {
    local db=$1

    if [ "${PODNAME}" == "" ]
    then
        PGOPTIONS="${PSQL_OPTIONS}" psql \
            -d "${db}" \
            -q \
            -v ON_ERROR_STOP=1
    else
        podman run --rm -i \
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
                    -v ON_ERROR_STOP=1
    fi
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
    if [ "${PODNAME}" != "" ]
    then
        CONTEXT_PREFIX="/tmp/context/"
    fi

    wait_for_postgres

    # header "pull latest postgres image"
    # podman pull "${IMAGE}"

    echo ""
    echo "** database ${DBNAME} **"
    run_single_script "database/maw_media.sql" "postgres" &> /dev/null

    header "full text search"
    queue "full-text-search/extension-dict_xsyn.sql"
    queue "full-text-search/dictionary-xsyn.sql"
    queue "full-text-search/dictionary-english_hunspell.sql"
    queue "full-text-search/dictionary-english.sql"

    header "roles"
    queue "roles/maw_media.sql"

    header "users"
    queue "users/svc_maw_media.sql"

    header "schemas"
    queue "schemas/media.sql"

    header "tables"
    queue "tables/media.scale.sql"
    queue "tables/media.place_kind.sql"
    queue "tables/media.place.sql"
    queue "tables/media.place_alias.sql"
    queue "tables/media.location.sql"
    queue "tables/media.point_of_interest.sql"
    queue "tables/media.type.sql"
    queue "tables/media.user.sql"
    queue "tables/media.role.sql"
    queue "tables/media.external_identity.sql"
    queue "tables/media.user_role.sql"
    queue "tables/media.category.sql"
    queue "tables/media.category_favorite.sql"
    queue "tables/media.media.sql"
    queue "tables/media.category_media.sql"
    queue "tables/media.file.sql"
    queue "tables/media.category_role.sql"
    queue "tables/media.comment.sql"
    queue "tables/media.favorite.sql"
    queue "tables/media.person_status.sql"
    queue "tables/media.person.sql"
    queue "tables/media.person_favorite.sql"
    queue "tables/media.clan.sql"
    queue "tables/media.clan_person.sql"
    queue "tables/media.face.sql"

    header "views"
    queue "views/media.category_search.sql"
    queue "views/media.category_stats.sql"
    queue "views/media.file_detail.sql"
    queue "views/media.media_detail.sql"
    queue "views/media.media_exif_gps.sql"
    queue "views/media.media_gps.sql"
    queue "views/media.media_location.sql"
    queue "views/media.user_category.sql"
    queue "views/media.user_media.sql"
    queue "views/media.user_face.sql"
    queue "views/media.user_location.sql"

    header "seed"
    queue "seed/media.type.sql"
    queue "seed/media.scale.sql"
    queue "seed/media.place_kind.sql"

    header "functions"
    queue "funcs/media.add_comment.sql"
    queue "funcs/media.assign_all_location_places.sql"
    queue "funcs/media.assign_location_place.sql"
    queue "funcs/media.build_place_slug.sql"
    queue "funcs/media.bulk_set_media_gps_override.sql"
    queue "funcs/media.create_clan.sql"
    queue "funcs/media.create_external_identity.sql"
    queue "funcs/media.delete_clan.sql"
    queue "funcs/media.delete_faces.sql"
    queue "funcs/media.delete_persons.sql"
    queue "funcs/media.favorite_category.sql"
    queue "funcs/media.favorite_media.sql"
    queue "funcs/media.favorite_person.sql"
    queue "funcs/media.fix_inaccurate_location.sql"
    queue "funcs/media.get_categories.sql"
    queue "funcs/media.get_categories_without_gps.sql"
    queue "funcs/media.get_category_media.sql"
    queue "funcs/media.get_category_years.sql"
    queue "funcs/media.get_clans.sql"
    queue "funcs/media.get_comments.sql"
    queue "funcs/media.get_face_exists.sql"
    queue "funcs/media.get_inaccurate_locations.sql"
    queue "funcs/media.get_is_admin.sql"
    queue "funcs/media.get_locations_without_metadata.sql"
    queue "funcs/media.get_media.sql"
    queue "funcs/media.get_media_faces.sql"
    queue "funcs/media.get_media_file.sql"
    queue "funcs/media.get_media_gps.sql"
    queue "funcs/media.get_metadata.sql"
    queue "funcs/media.get_person_categories.sql"
    queue "funcs/media.get_person_media.sql"
    queue "funcs/media.get_persons.sql"
    queue "funcs/media.get_place_ancestors.sql"
    queue "funcs/media.get_place_categories.sql"
    queue "funcs/media.get_place_descendants.sql"
    queue "funcs/media.get_place_media.sql"
    queue "funcs/media.get_places.sql"
    queue "funcs/media.get_random_media.sql"
    queue "funcs/media.get_scales.sql"
    queue "funcs/media.get_stats.sql"
    queue "funcs/media.get_stats_for_year.sql"
    queue "funcs/media.get_user_can_view_face.sql"
    queue "funcs/media.get_user_state.sql"
    queue "funcs/media.get_visible_person_count.sql"
    queue "funcs/media.normalize_place_name.sql"
    queue "funcs/media.resolve_place.sql"
    queue "funcs/media.search_categories.sql"
    queue "funcs/media.set_category_teaser.sql"
    queue "funcs/media.set_clan_persons.sql"
    queue "funcs/media.set_location_metadata.sql"
    queue "funcs/media.set_media_gps_override.sql"
    queue "funcs/media.set_point_of_interest.sql"
    queue "funcs/media.sync_faces.sql"
    queue "funcs/media.sync_person_statuses.sql"
    queue "funcs/media.sync_persons.sql"
    queue "funcs/media.update_clan.sql"

    header "post-deploy"
    queue "post-deploy/media.assign_all_location_places.sql"

    run_driver

    echo ""
    echo "** completed ${DBNAME} **"
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
