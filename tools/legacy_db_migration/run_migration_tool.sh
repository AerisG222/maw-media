#!/bin/bash

echo "Running migration tool - note that it may take some time as pip install can take some time..."

podman run -it --rm \
    --pod prod-maw-pod \
    --name pypg-migrate \
    --env-file "/home/svc_www_maw/maw_prod/podman-env/maw-postgres.env" \
    --volume "$(pwd):/scripts" \
    --volume "$(pwd):/output" \
    --security-opt label=disable \
    docker.io/library/python:3-bookworm \
        /scripts/_migrate.sh

echo "Completed!"
