#! /bin/bash
POD=integration-test-media-pod
TESTDIR="$(pwd)/media-testing/media-testing"

rm -rf "${TESTDIR}"

podman pod stop "${POD}"
podman pod rm "${POD}"
