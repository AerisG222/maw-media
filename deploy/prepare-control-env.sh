#!/bin/bash

# helper to prepare the controller node as documented in README.md
#
# versions are pinned, and installed with --upgrade, so this script converges an
# environment rather than only creating one.  it used to do neither: `pip install
# ansible` on an existing venv reports the requirement already satisfied and
# changes nothing, so a control node set up months ago stayed on whatever was
# current that day while a fresh one got the newest release.
#
# that drift is not cosmetic.  the playbook's real dependencies are the
# collections bundled with the ansible package - containers.podman for
# podman_login and podman_play, ansible.posix for authorized_key - and those move
# independently of anything written here.  pinning is what makes two control
# nodes deploy the same way, and makes a version bump a visible commit rather
# than a function of when somebody last ran this.
#
# to bump: raise the numbers below, re-run this, then confirm the playbook still
# passes `ansible-playbook --syntax-check` and `ansible-lint`.
#
# ansible 14.4.0 brings ansible-core 2.21.4, containers.podman 1.20.2 and
# ansible.posix 2.2.2; verified against this playbook on 2026-09-20.
set -e

python -m venv .venv

source .venv/bin/activate

# --upgrade-strategy eager, because --upgrade on its own only moves the packages
# named here.  pip leaves a dependency alone when the existing version still
# satisfies the requirement, so an upgraded environment kept ansible-lint 26.6.0
# where a fresh one installed 26.8.0 - the two would have diverged in exactly the
# way the pins are meant to prevent.  eager makes a converged venv match a newly
# created one.
pip install --upgrade --upgrade-strategy eager \
    'ansible==14.4.0' \
    'ansible-dev-tools==26.8.0' \
    'passlib==1.7.4'
