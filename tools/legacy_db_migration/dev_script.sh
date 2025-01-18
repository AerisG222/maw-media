podman run -it --rm \
    --pod dev-maw-pod \
    --name pypg-migrate-dev \
    --env-file /home/mmorano/maw_dev/podman-env/maw-postgres.env \
    --volume "$(pwd):/scripts" \
    --volume "$(pwd):output" \
    --security-opt label=disable \
    docker.io/library/python:3-bookworm \
        sh
