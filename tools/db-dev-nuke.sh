#!/bin/bash
podman pod stop pod-maw-media
podman rm dev-media-pg
sudo rm -rf ~mmorano/maw-media/dev/pg-data
