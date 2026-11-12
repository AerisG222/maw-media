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
# ansible-core is pinned on its own rather than through the ansible package,
# with the two collections the playbook uses installed beside it.  the package
# lags core: ansible 14.x requires core 2.21, and 2.21 can not run modules on
# python 3.15 - it reads dataclasses._is_type, which 3.15 removed, so every
# task on a fedora 45 host fails while gathering facts.  2.22.0 is the first
# release that handles it.  verified against this playbook on 2026-11-12.
#
# when an ansible package release brings core 2.22 or later, going back to
# pinning that is fine - it bundles the same collections.
set -e

# --clear, so a venv made by an older python is rebuilt rather than left with the
# packages of both - a fedora upgrade moves `python` to the next version
python -m venv --clear .venv

source .venv/bin/activate

# --upgrade-strategy eager, because --upgrade on its own only moves the packages
# named here.  pip leaves a dependency alone when the existing version still
# satisfies the requirement, so an upgraded environment kept ansible-lint 26.6.0
# where a fresh one installed 26.8.0 - the two would have diverged in exactly the
# way the pins are meant to prevent.  eager makes a converged venv match a newly
# created one.
pip install --upgrade --upgrade-strategy eager \
    'ansible-core==2.22.0' \
    'ansible-dev-tools==26.8.0' \
    'passlib==1.7.4'

# into the venv's own site-packages, which ansible-core already searches, so the
# collections come and go with the venv like the packages above, and nothing
# installed under ~/.ansible by hand can stand in for them
ansible-galaxy collection install --upgrade \
    --collections-path "$(python -c 'import sysconfig; print(sysconfig.get_paths()["purelib"])')" \
    'containers.podman:==1.20.2' \
    'ansible.posix:==2.2.2'
