#!/bin/bash
podman pod stop dev-api-pod
podman rm pg-api-dev
sudo rm -rf ~mmorano/maw-api-dev/data/pgdata
