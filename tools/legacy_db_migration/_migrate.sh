#!/bin/bash

pip install psycopg2-binary uuid-utils
python3 /scripts/_migrate_media.py
