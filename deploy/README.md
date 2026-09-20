# Notes for Deployment

To deploy the application, an ansible playbook is provided to easily deploy this site to
one or more nodes. It makes a couple of key assumptions:

1. Remote OS is Linux and has SSH running that can be accessed from the machine where the scripts run.
2. This playbook only focuses on getting this application running in a podman pod, and is expected that a different deployment will be made to serve as the reverse proxy (i.e. maw-gateway).
3. Trying to keep this simple for now - I went crazy building out an advanced playbook last time, but there are so many steps and I don't need to run often, so I rather not run it and don't want to revamp it for the new solution.
4. Once deployed, you currently will still need to run the following steps manually:
    1. run the apply-migration step to load data from legacy database (/tools/legacy_db_migration)
    2. set the password for the svc_maw_www db role

## Deploying the Database Schema

The schema lives in `src/db-postgres/` and is deployed by the same playbook, but
only when asked for by tag:

```
./deploy.sh              # application only - never touches the database
                         # (answer the environment prompt as usual)
```

To deploy the schema, run the playbook directly with `--tags db`:

```
source .venv/bin/activate
ansible-playbook \
    --become-password-file "~/maw-media/${MAW_ENV}/ansible/become-password-file" \
    --inventory "inventories/${MAW_ENV}.yml" \
    --extra-vars "@~/maw-media/${MAW_ENV}/ansible/vars.yml" \
    --extra-vars "mawenv=${MAW_ENV}" \
    --tags db \
    maw-media-playbook.yml
```

That stages `src/db-postgres/` onto the target and runs `deploy.sh` there against
the pod, so there is no longer any need to copy the directory by hand.

**Run it before the application deploy** when a release contains both, so the api
never starts against a schema older than its code.

### Why it is opt-in rather than part of every run

The schema scripts are all re-runnable, but running them is neither free nor
silent, and none of it belongs in a routine container image bump:

* `views/media.category_search.sql` drops and rebuilds a materialized view over
  the whole library, and the view is missing while that happens
* `views/media.user_media.sql` does a `DROP VIEW ... CASCADE` that takes
  `user_face` and `user_location` with it
* `post-deploy/media.assign_all_location_places.sql` walks every geocoded
  location
* every `ALTER TABLE` takes an `ACCESS EXCLUSIVE` lock, which ordinary reads
  block on - `deploy.sh` sets `lock_timeout=30s`, so a blocked deploy fails
  rather than hangs

### Pre-deploy dumps

A `pg_dump -Fc` is taken immediately before the schema deploy and written to
`~/maw-media/pg-predeploy/` on the target, named
`maw_media.predeploy.<timestamp>.dump`. The five most recent are kept.

This is deliberately **not** `pg-backups/`: the nightly rclone job runs
`sync /backups`, so a dump left there would be uploaded to Drive, and then
deleted from Drive again as soon as it was pruned locally. These are local undo
buttons for a deploy that went wrong; `pg-backups/` is the archive.

To roll back, restore the dump taken just before the deploy:

```
~/maw-media/scripts/logs.sh                    # confirm what went wrong first
podman exec -i pod-maw-media-maw-media-postgres \
    pg_restore -h localhost -U postgres -d maw_media --clean --if-exists \
    < ~/maw-media/pg-predeploy/maw_media.predeploy.<timestamp>.dump
```

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
