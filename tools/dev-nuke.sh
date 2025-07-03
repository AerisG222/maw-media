#!/bin/bash
podman pod stop dev-media-pod
podman rm dev-media-pg
sudo rm -rf ~mmorano/maw/dev/media/data/pgdata
