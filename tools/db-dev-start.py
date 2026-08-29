#!/usr/bin/env python3
import os
import subprocess
from podman import PodmanClient

POD = 'pod-maw-media'
PG_IMG = 'docker.io/library/postgres:18-trixie'
PG_CONTAINER = 'dev-media-pg'
DATADIR = '/home/mmorano/maw-media/dev'
PGDATA = f"{DATADIR}/pg-data"
PGPWD = f"{DATADIR}/pg-secrets"

# the full text search config needs the hunspell dictionaries and the xsyn rules
# to live in the image's $SHAREDIR/tsearch_data.  they are mounted in file by
# file rather than as a directory - mounting the directory would hide the stock
# contents, and the english_hunspell dictionary is declared with
# StopWords = english, which reads english.stop from there.
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TSEARCH_SRC = f"{REPO_ROOT}/src/db-postgres/tsearch_data"
TSEARCH_DEST = '/usr/share/postgresql/18/tsearch_data'
TSEARCH_FILES = ['en_us.dict', 'en_us.affix', 'maw_media_xsyn.rules']

# make sure we start the podman service which is needed by the python api
subprocess.run(['systemctl', '--user', 'start', 'podman.socket'])

client = PodmanClient()

if not client.pods.exists(POD):
    client.pods.create(
        POD,
        portmappings = [
            {
                'container_port': 5432,
                'host_port': 6543
            }
        ]
    )

pod = client.pods.get(POD)
pod.start()

if not os.path.exists(PGDATA):
    os.makedirs(PGDATA)

client.images.pull(PG_IMG)

if not client.containers.exists(PG_CONTAINER):
    client.containers.create(
        image = PG_IMG,
        name = PG_CONTAINER,
        pod = POD,
        # userns_mode = 'keep-id:uid=999',
        environment = {
            "POSTGRES_PASSWORD_FILE": f"/secrets/psql-postgres"
        },
        mounts = [
            {
                'type': 'bind',
                'source': PGDATA,
                'target': '/var/lib/postgresql',
                'read_only': False,
                'relabel': 'Z'
            },
            {
                'type': 'bind',
                'source': PGPWD,
                'target': '/secrets',
                'read_only': True,
                'relabel': 'Z'
            }
        ] + [
            {
                'type': 'bind',
                'source': f"{TSEARCH_SRC}/{f}",
                'target': f"{TSEARCH_DEST}/{f}",
                'read_only': True,
                'relabel': 'z'
            }
            for f in TSEARCH_FILES
        ]
    )

container = client.containers.get(PG_CONTAINER)
container.start()
