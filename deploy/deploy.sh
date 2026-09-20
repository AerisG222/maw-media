#!/bin/bash
get_value() {
    local prompt=$1
    local secure=$2
    local default=$3
    local val=

    while [ "${val}" = "" ]
    do
        if [ "${secure}" = "y" ]; then
            read -e -r -s -p "${prompt}" val
        else
            read -e -r -p "${prompt}" val
        fi

        if [ "${val}" = "" -a "${default}" != "" ]; then
            val="${default}"
        fi
    done

    echo "${val}"
}

get_file() {
    local prompt=$1
    local file=$2

    while [ "${file}" = "" -o ! -f "${file}" ]
    do
        file=$(get_value "${prompt}" 'n')
    done

    echo "${file}"
}

get_maw_env() {
    local env=

    while [ "${env}" = "" ]
    do
        env=$(get_value 'Enter deployment environment [staging | prod]: ' 'n')

        if [ "${env}" != "staging" -a "${env}" != "prod" ]; then
            env=
        fi
    done

    echo "${env}"
}

get_deploy_target() {
    local target=

    while [ "${target}" = "" ]
    do
        target=$(get_value 'Enter what to deploy [app | db | both]: ' 'n' 'app')

        if [ "${target}" != "app" -a "${target}" != "db" -a "${target}" != "both" ]; then
            target=
        fi
    done

    echo "${target}"
}

# one place where the playbook is invoked, so the inventory, the password file
# and the vars file are named once rather than per caller.
#
# the schema deploy is the same playbook with --tags db - see the tagging note in
# maw-media-playbook.yml for why it is opt in - so the only thing that varies
# here is whether that tag is passed.
run_playbook() {
    local tags=$1
    local -a tag_args=()

    if [ "${tags}" != "" ]; then
        tag_args=(--tags "${tags}")
    fi

    # the next line may be needed if sshkeys are not configured on target
    #--connection-password-file ~/maw-www/staging/ansible/connection-password-file \

    ansible-playbook \
        --become-password-file "~/maw-media/${MAW_ENV}/ansible/become-password-file" \
        --inventory "inventories/${MAW_ENV}.yml" \
        --extra-vars "@~/maw-media/${MAW_ENV}/ansible/vars.yml" \
        --extra-vars "mawenv=${MAW_ENV}" \
        "${tag_args[@]}" \
        maw-media-playbook.yml
}

if [ ! -d '.venv' ]; then
    echo 'Please run the prepare script first first!'
    exit
else
    source .venv/bin/activate
fi

MAW_ENV=$(get_maw_env)
DEPLOY_TARGET=$(get_deploy_target)

if [ "${DEPLOY_TARGET}" = "db" -o "${DEPLOY_TARGET}" = "both" ]; then
    echo ""
    echo "** deploying database schema to ${MAW_ENV} **"

    if ! run_playbook "db"; then
        echo "" >&2
        echo "** DATABASE DEPLOY FAILED - the application was not deployed **" >&2
        echo "** a pre-deploy dump is in ~/maw-media/pg-predeploy on the target **" >&2
        exit 1
    fi
fi

# the schema goes first when both are asked for, so the api never starts against
# a schema older than the code that expects it
if [ "${DEPLOY_TARGET}" = "app" -o "${DEPLOY_TARGET}" = "both" ]; then
    echo ""
    echo "** deploying application to ${MAW_ENV} **"

    run_playbook ""
fi
