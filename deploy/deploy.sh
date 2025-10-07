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

if [ ! -d '.venv' ]; then
    echo 'Please run the prepare script first first!'
    exit
else
    source .venv/bin/activate
fi

MAW_ENV=$(get_maw_env)

# the next line may be needed if sshkeys are not configured on target
#--connection-password-file ~/maw-www/staging/ansible/connection-password-file \

ansible-playbook \
    --become-password-file "~/maw-media/${MAW_ENV}/ansible/become-password-file" \
    --inventory "inventories/${MAW_ENV}.yml" \
    --extra-vars "@~/maw-media/${MAW_ENV}/ansible/vars.yml" \
    --extra-vars "mawenv=${MAW_ENV}" \
    maw-media-playbook.yml
