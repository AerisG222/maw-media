#!/bin/bash

# helper to prepare the controller node as documented in README.md
python -m venv .venv

source .venv/bin/activate

pip install \
    ansible \
    ansible-dev-tools \
    passlib
