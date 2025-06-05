#!/usr/bin/env python3
import os
import subprocess
from podman import PodmanClient

POD = 'dev-media-pod'
PG_CONTAINER = 'dev-media-pg'
DATADIR = '/home/mmorano/maw/dev/media/data'
PGDATA = f"{DATADIR}/pgdata"
PGPWD = f"{DATADIR}/pgpwd"

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

if not client.containers.exists(PG_CONTAINER):
    client.containers.create(
        image = 'docker.io/library/postgres:17',
        name = PG_CONTAINER,
        pod = POD,
        environment = {
            "POSTGRES_PASSWORD_FILE": f"/secrets/psql-postgres"
        },
        mounts = [
            {
                'type': 'bind',
                'source': PGDATA,
                'target': '/var/lib/postgresql/data',
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
        ]
    )

container = client.containers.get(PG_CONTAINER)
container.start()
