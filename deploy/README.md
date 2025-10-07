# Notes for Deployment

To deploy the application, an ansible playbook is provided to easily deploy this site to
one or more nodes. It makes a couple of key assumptions:

1. Remote OS is Linux and has SSH running that can be accessed from the machine where the scripts run.
2. This playbook only focuses on getting this application running in a podman pod, and is expected that a different deployment will be made to serve as the reverse proxy (i.e. maw-gateway).
3. Trying to keep this simple for now - I went crazy building out an advanced playbook last time, but there are so many steps and I don't need to run often, so I rather not run it and don't want to revamp it for the new solution.
4. Once deployed, you currently will still need to run the following steps manually:
    1. run the db deploy steps (src/db-postgres/deploy.sh) to create the db
    2. run the apply-migration step to load data from legacy database (/tools/legacy_db_migration)
    3. set the password for the svc_maw_www db role

## Control Node Setup

1. Make sure python/pip are installed
2. `cd deploy/` within project
3. Setup a python environment for ansible in the deploy dir: `python -m venv .venv`
4. Enter the environment: `source .venv/bin/activate`
5. If you want to exit that environment, just run `deactivate`
6. Install Ansible
    1. `pip install ansible`
    2. `pip install passlib`
    3. to upgrade: `pip install --upgrade ansible`

## Managed Node Setup

1. Python needs to be installed (note: ansible is agentless, so that does not need to be installed on managed nodes)
