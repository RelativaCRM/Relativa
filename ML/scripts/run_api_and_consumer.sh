#!/usr/bin/env bash
set -euo pipefail

python manage.py run_domain_consumer &
exec python manage.py runserver "0.0.0.0:8084"
