#!/bin/bash
#
# regenerates the hunspell dictionary files under tsearch_data/.
#
# postgres only reads text search dictionaries from $SHAREDIR/tsearch_data, and
# the ispell template needs the hunspell en_US dictionary converted into the
# .dict/.affix pair it expects.  that conversion is what pg_updatedicts does, so
# this runs it once inside the stock postgres image and streams the result back
# out.  the output is committed, which keeps the running container on the
# official image - the files are mounted in rather than baked into a custom one.
#
# rerun this when the postgres major version changes or to pick up a newer
# hunspell-en-us, and commit the diff.
#
# maw_media_xsyn.rules lives in tsearch_data/ as well but is hand maintained -
# nothing here touches it.
set -euo pipefail

IMAGE="docker.io/library/postgres:18-trixie"
OUTDIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/tsearch_data"

# the build noise goes to stderr so that stdout carries nothing but the tar
# stream.  tar rather than a bind mount, so the files come out owned by the
# caller instead of by a subuid from the container's user namespace.
podman run --rm "${IMAGE}" bash -c '
    set -eu
    { apt-get update
      apt-get install -y --no-install-recommends hunspell-en-us
      /usr/sbin/pg_updatedicts
    } 1>&2
    # -h: pg_updatedicts leaves symlinks into /var/cache, we want the content
    tar -ch -C "$(pg_config --sharedir)/tsearch_data" en_us.dict en_us.affix
' | tar -x -C "${OUTDIR}" -v

echo "wrote to ${OUTDIR}"
