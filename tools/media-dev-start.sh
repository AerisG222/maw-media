#!/bin/bash
# this script ensures we always load the latest env vars before trying to run the dev site
# this also keeps these confined to this script, so they won't leak into regular environment
ENV_FILE="/home/mmorano/maw-media/dev/env/maw-media.env"

function load_dotenv(){
    # https://stackoverflow.com/a/66118031/134904
    source <(cat $1 | sed -e '/^#/d;/^\s*$/d' -e "s/'/'\\\''/g" -e "s/=\(.*\)/='\1'/g")
}

set -o allexport
[ -f "${ENV_FILE}" ] && load_dotenv "${ENV_FILE}"
set +o allexport

dotnet run --project ../src/MawMedia/MawMedia.csproj
