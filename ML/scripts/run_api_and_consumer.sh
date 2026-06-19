#!/usr/bin/env bash
set -euo pipefail

python manage.py run_domain_consumer &
python manage.py run_recalculate_consumer &
python manage.py run_graph_score_consumer &
exec python manage.py runserver "0.0.0.0:8084"
